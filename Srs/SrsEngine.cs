using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using mindvault.Core.Logging;
using mindvault.Services;

namespace mindvault.Srs
{
    // We want to refactor the existing SrsEngine to use a "dual-mode" scheduler: DEFAULT mode and CRAM mode.
    // DEFAULT mode: uses a 30-minute "due-soon window," batch up to 10 cards; when fewer than 10 due, fill with new cards (unlimited).
    // On wrong answer in DEFAULT mode: reschedule the card soon (e.g. +5 minutes).
    // On correct answer in DEFAULT mode: schedule next review with spaced intervals (e.g. 1 day ? 3 days ? 7 days ? 14 days ? �).
    //
    // CRAM mode: uses a 10-minute "due-soon window," batch up to 5 cards; when fewer than 5 due, fill with new cards (unlimited).
    // On wrong answer in CRAM mode: reschedule soon (e.g. +1 minute).
    // On correct answer in CRAM mode: schedule next review after a short cooldown (e.g. 30�60 minutes).
    //
    // Replace RefreshActiveSet and scheduling logic accordingly, and ensure PickNextCard picks from new active set sorted by DueAt.
    // Remove or ignore old NEW_CARD_LIMIT_DEFAULT / INTRODUCE_PER_REFRESH caps � new-card introduction is unlimited (as long as batch slots exist).

    /// <summary>
    /// Spaced repetition engine with deterministic SM-2 scheduling and a fast cram mode.
    /// Card order is strictly by DueAt; no randomness or jitter is used.
    /// </summary>
    public class SrsEngine
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly List<SrsCard> _cards = new();
        private readonly Queue<SrsCard> _recentlyShown = new();
        private readonly HashSet<SrsCard> _learnedEver = new();
        private const int RECENT_BUFFER_SIZE = 5;

        private const int ACTIVE_LIMIT_DEFAULT = 10;
        private const int ACTIVE_LIMIT_CRAM = 8; // softened increase; will cap to 5 via window batch
        private const int DEFAULT_BATCH_LIMIT = 5; // reduced default batch size from 10 to 5
        private const int CRAM_BATCH_LIMIT = 5;
        private static readonly TimeSpan DEFAULT_DUE_WINDOW = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan CRAM_DUE_WINDOW = TimeSpan.FromMinutes(5); // lowered limiter to 5 minutes

        private List<SrsCard> _activeSet = new();
        private bool _cramMode = false;
        private int _newIntroducedThisSession = 0; // ignored for quota, kept for telemetry if needed

        private readonly ICoreLogger _logger;
        private readonly DatabaseService? _db;

        private const double MinEase = 1.3;
        private const double MaxEase = 3.0;
        private const int FailThresholdQuality = 3;
        private const double CRAM_GROWTH_RATIO = 2.0; // multiplicative growth for cram mode intervals

        private int _saveCounter = 0;
        private DateTime _lastSaveTime = DateTime.UtcNow;
        private const string PrefReviewStatePrefix = "ReviewState_";

        public SrsCard? CurrentCard { get; private set; }
        public int ReviewerId { get; private set; }
        public int CorrectCount { get; private set; }
        public int WrongCount { get; private set; }
        public bool SessionComplete { get; private set; }

        public int Total => _cards.Count;
        public int Seen => _cards.Count(c => c.SeenCount > 0);
        public int Learned => _learnedEver.Count;
        public int Skilled => _cards.Count(c => c.CountedSkilled);
        public int Memorized => _cards.Count(c => c.CountedMemorized);
        public int ActiveCount => _activeSet.Count;

        public event Action? StateChanged;

        private void RefreshActiveSet()
        {
            var now = DateTime.UtcNow;
            var window = _cramMode ? CRAM_DUE_WINDOW : DEFAULT_DUE_WINDOW;
            int batchLimit = _cramMode ? CRAM_BATCH_LIMIT : DEFAULT_BATCH_LIMIT;

            // 1) Due-soon cards (non-Avail) within window
            var dueSoon = _cards
                .Where(c => c.Stage != Stage.Avail && c.DueAt <= now + window)
                .OrderBy(c => c.DueAt)
                .Take(batchLimit)
                .ToList();

            var batch = new List<SrsCard>(dueSoon);
            int slots = batchLimit - batch.Count;

            // 2) Fill remaining slots with new cards (unlimited intro)
            if (slots > 0)
            {
                var newCards = _cards
                    .Where(c => c.Stage == Stage.Avail)
                    .OrderBy(c => c.Id)
                    .Take(slots)
                    .ToList();
                foreach (var c in newCards)
                {
                    c.Stage = Stage.Seen;
                    c.Repetitions = 0;
                    c.Interval = TimeSpan.Zero;
                    c.DueAt = now;
                    c.CooldownUntil = now;
                    batch.Add(c);
                    _newIntroducedThisSession++;
                }
            }

            // 3) Fallback: If batch is still empty AND there are no new cards available,
            // get the earliest due card from non-Avail cards (even if not due yet)
            if (batch.Count == 0 && !_cards.Any(c => c.Stage == Stage.Avail))
            {
                var earliestDueCard = _cards
                    .Where(c => c.Stage != Stage.Avail) // Only consider cards that have been seen
                    .OrderBy(c => c.DueAt) // Get the earliest due card regardless of due time
                    .FirstOrDefault();

                if (earliestDueCard != null)
                {
                    batch.Add(earliestDueCard);
                }
            }

            _activeSet = batch.OrderBy(c => c.DueAt).ToList();
        }

        public async Task LoadCardsAsync(int reviewerId, Func<int, Task<IEnumerable<SrsCard>>> fetchFunc)
        {
            await _lock.WaitAsync();
            try
            {
                _cramMode = false; ReviewerId = reviewerId; _newIntroducedThisSession = 0;
                _cards.Clear(); _learnedEver.Clear(); CorrectCount = 0; WrongCount = 0; _recentlyShown.Clear(); _activeSet.Clear(); CurrentCard = null;
                var loaded = await fetchFunc(reviewerId);
                _cards.AddRange(loaded);
                RestoreProgress();
                RefreshActiveSet();
                _logger.Info($"Loaded {Total} cards (default mode)", "srs");
            }
            catch (Exception ex) { _logger.Error("LoadCardsAsync failed", ex, "srs"); }
            finally { _lock.Release(); }
        }

        public async Task LoadCardsForCramModeAsync(int reviewerId, Func<int, Task<IEnumerable<SrsCard>>> fetchFunc)
        {
            await _lock.WaitAsync();
            try
            {
                _cramMode = true; ReviewerId = reviewerId; _newIntroducedThisSession = 0;
                _cards.Clear(); _learnedEver.Clear(); CorrectCount = 0; WrongCount = 0; _recentlyShown.Clear(); _activeSet.Clear(); CurrentCard = null;
                var loaded = await fetchFunc(reviewerId);
                _cards.AddRange(loaded);
                RestoreProgress();
                RefreshActiveSet();
                _logger.Info($"Loaded {Total} cards (cram mode)", "srs");
            }
            catch (Exception ex) { _logger.Error("LoadCardsForCramModeAsync failed", ex, "srs"); }
            finally { _lock.Release(); }
        }

        // Overload to satisfy existing call sites with CramModeOptions
        public Task LoadCardsForCramModeAsync(int reviewerId, Func<int, Task<IEnumerable<SrsCard>>> fetchFunc, CramModeOptions _)
            => LoadCardsForCramModeAsync(reviewerId, fetchFunc);

        private void ScheduleAfterAnswer(SrsCard card, bool success)
        {
            var now = DateTime.UtcNow;
            if (_cramMode)
            {
                if (!success)
                {
                    // wrong ? retry soon (1 min) and reset progression; add short cooldown to prevent instant repeat
                    card.Repetitions = 0;
                    card.Interval = TimeSpan.FromMinutes(1);
                    card.DueAt = now + card.Interval;
                    card.CooldownUntil = now + TimeSpan.FromSeconds(10);
                }
                else
                {
                    card.Repetitions++;
                    if (card.Repetitions == 1)
                    {
                        // start at 3 minutes
                        card.Interval = TimeSpan.FromMinutes(3);
                    }
                    else
                    {
                        var baseSeconds = card.Interval.TotalSeconds <= 0 ? TimeSpan.FromMinutes(3).TotalSeconds : card.Interval.TotalSeconds;
                        card.Interval = TimeSpan.FromSeconds(baseSeconds * CRAM_GROWTH_RATIO);
                    }
                    card.DueAt = now + card.Interval;
                    card.CooldownUntil = now + TimeSpan.FromSeconds(5);
                }
            }
            else
            {
                if (!success)
                {
                    // wrong ? small delay (5 min) and cooldown to avoid immediate repeat
                    card.Repetitions = 0;
                    card.Interval = TimeSpan.FromMinutes(5);
                    card.DueAt = now + card.Interval;
                    card.CooldownUntil = now + TimeSpan.FromSeconds(10);
                }
                else
                {
                    card.Repetitions++;
                    TimeSpan next;
                    switch (card.Repetitions)
                    {
                        case 1: next = TimeSpan.FromDays(1); break;
                        case 2: next = TimeSpan.FromDays(3); break;
                        case 3: next = TimeSpan.FromDays(7); break;
                        case 4: next = TimeSpan.FromDays(14); break;
                        default: next = TimeSpan.FromDays(30); break;
                    }
                    card.Interval = next;
                    card.DueAt = now + next;
                    card.CooldownUntil = now + TimeSpan.FromSeconds(5);
                }
            }
        }

        private void SaveProgressThrottled()
        {
            if (_saveCounter % 5 == 0 || (DateTime.UtcNow - _lastSaveTime).TotalSeconds > 15)
            { SaveProgress(); _lastSaveTime = DateTime.UtcNow; }
        }

        public async Task GradeCardAsync(bool success) => await GradeCardWithQualityAsync(success ? 5 : 2);
        public async Task GradeCardForCramModeAsync(bool success) { if (!_cramMode) { await GradeCardAsync(success); return; } await GradeCardAsync(success); }

        private void AdvanceStageOnSuccess(SrsCard card)
        {
            // Keep existing stage progression semantics
            if (card.Stage == Stage.Seen)
            {
                if (!card.CorrectOnce) card.CorrectOnce = true;
                else { card.CorrectOnce = false; card.Stage = Stage.Learned; _learnedEver.Add(card); }
            }
            else if (card.Stage == Stage.Learned) { card.Stage = Stage.Skilled; card.CountedSkilled = true; }
            else if (card.Stage == Stage.Skilled) { card.Stage = Stage.Memorized; card.CountedMemorized = true; }
        }

        private async Task GradeCardWithQualityAsync(int quality)
        {
            await _lock.WaitAsync();
            try
            {
                if (CurrentCard == null) return;
                var card = CurrentCard;
                bool success = quality >= FailThresholdQuality;

                // Ease adjustments kept minimal
                if (success)
                {
                    card.Ease = Math.Clamp(card.Ease + (_cramMode ? 0.05 : 0.1), MinEase, MaxEase);
                    AdvanceStageOnSuccess(card);
                }
                else
                {
                    card.Ease = Math.Max(MinEase, card.Ease - (_cramMode ? 0.1 : 0.2));
                    // On wrong from learned+, demote to Seen and clear counters
                    bool wasLearnedOrAbove = card.Stage >= Stage.Learned;
                    if (card.Stage != Stage.Avail)
                    {
                        card.CorrectOnce = false; card.CountedSkilled = false; card.CountedMemorized = false;
                        if (wasLearnedOrAbove) card.Stage = Stage.Seen;
                    }
                    // Ensure Learned counter updates immediately
                    if (wasLearnedOrAbove)
                    {
                        _learnedEver.Remove(card);
                    }
                }

                ScheduleAfterAnswer(card, success);
                _saveCounter++; SaveProgressThrottled();
                RefreshActiveSet();
            }
            catch (Exception ex) { _logger.Warn("GradeCardAsync error: " + ex.Message, "srs"); }
            finally { _lock.Release(); StateChanged?.Invoke(); }
        }

        public async Task SkipCardAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (CurrentCard != null)
                {
                    var now = DateTime.UtcNow;
                    CurrentCard.DueAt = now + (_cramMode ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(5));
                }
            }
            catch (Exception ex) { _logger.Warn("SkipCard error: " + ex.Message, "srs"); }
            finally { _lock.Release(); RefreshActiveSet(); PickNextCard(); }
        }

        private bool ShouldMarkSessionComplete() => _cards.Count > 0 && _cards.All(c => c.Stage >= Stage.Learned);

        public void PickNextCard()
        {
            _lock.Wait();
            try
            {
                var now = DateTime.UtcNow;
                if (_activeSet.Count == 0) RefreshActiveSet();

                // Pick first eligible by due time and cooldown, avoid immediate repeats
                SrsCard? next = _activeSet.FirstOrDefault(c => c.CooldownUntil <= now && !_recentlyShown.Contains(c));
                if (next == null)
                {
                    next = _activeSet.FirstOrDefault(c => c.CooldownUntil <= now) ?? null;
                }
                if (next == null)
                {
                    // If still none, try to introduce a single new card and pick it
                    var avail = _cards.FirstOrDefault(c => c.Stage == Stage.Avail);
                    if (avail != null)
                    {
                        avail.Stage = Stage.Seen; avail.Repetitions = 0; avail.Interval = TimeSpan.Zero; avail.DueAt = now; avail.CooldownUntil = now;
                        next = avail; RefreshActiveSet();
                        _newIntroducedThisSession++;
                    }
                }

                // As a final fallback: pick earliest non-new card that is not on cooldown
                if (next == null && _cards.Count > 0)
                {
                    next = _cards
                        .Where(c => c.Stage != Stage.Avail && c.CooldownUntil <= now)
                        .OrderBy(c => c.DueAt)
                        .FirstOrDefault();
                    if (next == null)
                    {
                        // Try any card that is not on cooldown
                        next = _cards.Where(c => c.CooldownUntil <= now).OrderBy(c => c.DueAt).FirstOrDefault();
                    }
                }

                if (next == null) { CurrentCard = null; SessionComplete = ShouldMarkSessionComplete(); return; }
                CurrentCard = next;
                _recentlyShown.Enqueue(next);
                if (_recentlyShown.Count > RECENT_BUFFER_SIZE) _recentlyShown.Dequeue();
                CurrentCard.SeenCount++;
                SessionComplete = ShouldMarkSessionComplete();
            }
            catch (Exception ex) { _logger.Warn("PickNextCard error: " + ex.Message, "srs"); }
            finally { _lock.Release(); StateChanged?.Invoke(); }
        }

        // Build a stable persistence key for the current deck that doesn't change on rename.
        // Uses a deterministic string of sorted card IDs when available; falls back to ReviewerId.
        private string GetReviewStateKey()
        {
            // Tie progress strictly to the reviewer id so adds/deletes/renames don't reset state
            return PrefReviewStatePrefix + ReviewerId;
        }

        public void SaveProgress()
        {
            try
            {
                if (_cards == null || _cards.Count == 0) return;
                var payload = _cards.Select(c => new
                {
                    c.Id,
                    Stage = c.Stage.ToString(),
                    c.DueAt,
                    c.CooldownUntil,
                    c.Ease,
                    IntervalDays = c.Interval.TotalDays,
                    c.CorrectOnce,
                    c.ConsecutiveCorrects,
                    c.CountedSkilled,
                    c.CountedMemorized,
                    c.SeenCount,
                    c.Repetitions
                }).ToList();
                
                var json = JsonSerializer.Serialize(payload);
                
                // Use file storage instead of Preferences to avoid Windows 8KB limit
                var filePath = GetProgressFilePath();
                File.WriteAllText(filePath, json);
                Debug.WriteLine($"[SrsEngine] Progress saved to file: {filePath} ({json.Length} bytes)");

                // Update database Learned field for cards that have reached Learned stage
                if (_db != null && ReviewerId > 0)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            foreach (var card in _cards.Where(c => c.Stage >= Stage.Learned))
                            {
                                await _db.UpdateFlashcardLearnedAsync(card.Id, true);
                            }
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex) { _logger.Warn("SaveProgress error: " + ex.Message, "srs"); Debug.WriteLine($"[SrsEngine] SaveProgress error: {ex.Message}"); }
        }

        /// <summary>
        /// Gets the file path for storing progress data for the current reviewer.
        /// Uses AppDataDirectory which has no size limits.
        /// </summary>
        private string GetProgressFilePath()
        {
            var progressDir = Path.Combine(FileSystem.AppDataDirectory, "Progress");
            if (!Directory.Exists(progressDir))
            {
                Directory.CreateDirectory(progressDir);
            }
            return Path.Combine(progressDir, $"ReviewState_{ReviewerId}.json");
        }

        private void RestoreProgress()
        {
            try
            {
                string? payload = null;
                var filePath = GetProgressFilePath();
                
                // First, try to read from file storage (new format)
                if (File.Exists(filePath))
                {
                    payload = File.ReadAllText(filePath);
                    Debug.WriteLine($"[SrsEngine] Progress loaded from file: {filePath}");
                }
                else
                {
                    // Fallback: try legacy Preferences storage and migrate if found
                    payload = Preferences.Get(GetReviewStateKey(), null);
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        Debug.WriteLine($"[SrsEngine] Migrating progress from Preferences to file storage");
                        // Migrate to file storage
                        try
                        {
                            var progressDir = Path.Combine(FileSystem.AppDataDirectory, "Progress");
                            if (!Directory.Exists(progressDir))
                            {
                                Directory.CreateDirectory(progressDir);
                            }
                            File.WriteAllText(filePath, payload);
                            // Remove from Preferences after successful migration
                            Preferences.Remove(GetReviewStateKey());
                            Debug.WriteLine($"[SrsEngine] Migration complete, legacy preference removed");
                        }
                        catch (Exception migEx)
                        {
                            Debug.WriteLine($"[SrsEngine] Migration failed: {migEx.Message}");
                        }
                    }
                }
                
                if (string.IsNullOrWhiteSpace(payload)) return;
                var list = JsonSerializer.Deserialize<List<JsonElement>>(payload);
                if (list == null) return;
                foreach (var dto in list)
                {
                    var id = dto.GetProperty("Id").GetInt32();
                    var card = _cards.FirstOrDefault(x => x.Id == id);
                    if (card == null) continue;
                    card.Stage = Enum.TryParse<Stage>(dto.GetProperty("Stage").GetString(), out Stage st) ? st : Stage.Seen;
                    card.DueAt = dto.GetProperty("DueAt").GetDateTime();
                    card.CooldownUntil = dto.TryGetProperty("CooldownUntil", out var cd) ? cd.GetDateTime() : card.DueAt;
                    card.Ease = dto.GetProperty("Ease").GetDouble();
                    card.Interval = TimeSpan.FromDays(dto.GetProperty("IntervalDays").GetDouble());
                    card.CorrectOnce = dto.GetProperty("CorrectOnce").GetBoolean();
                    card.ConsecutiveCorrects = dto.GetProperty("ConsecutiveCorrects").GetInt32();
                    card.CountedSkilled = dto.GetProperty("CountedSkilled").GetBoolean();
                    card.CountedMemorized = dto.GetProperty("CountedMemorized").GetBoolean();
                    card.SeenCount = dto.TryGetProperty("SeenCount", out var sc) ? sc.GetInt32() : card.SeenCount;
                    card.Repetitions = dto.TryGetProperty("Repetitions", out var rep) ? rep.GetInt32() : 0;
                    if (card.Stage >= Stage.Learned) { _learnedEver.Add(card); if (card.Stage >= Stage.Skilled) card.CountedSkilled = true; if (card.Stage == Stage.Memorized) card.CountedMemorized = true; }
                }
                Debug.WriteLine($"[SrsEngine] Restored progress for {list.Count} cards");
            }
            catch (Exception ex) { _logger.Warn("RestoreProgress error: " + ex.Message, "srs"); Debug.WriteLine($"[SrsEngine] RestoreProgress error: {ex.Message}"); }
        }

        public SrsEngine(ICoreLogger logger, DatabaseService? db = null)
        {
            _logger = logger;
            _db = db;
        }

        // Parameterless constructor for callers that don't provide a logger (creates a no-op logger).
        public SrsEngine() : this(new mindvault.Core.Logging.NullCoreLogger(), null) { }
    }
}