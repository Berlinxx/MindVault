using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace mindvault.Srs
{
    /// <summary>
    /// Core spaced repetition engine (deck-independent logic)
    /// </summary>
    public class SrsEngine
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly List<SrsCard> _cards = new();
        private readonly Queue<SrsCard> _recentlyShown = new();
        private readonly HashSet<SrsCard> _learnedEver = new();
        private const int RECENT_BUFFER_SIZE = 5;
        private const int DEFAULT_BATCH_SIZE = 10;
        private int _batchSize = DEFAULT_BATCH_SIZE;
        private List<SrsCard> _activeBatch = new();
        private int _batchIndex = 0; // starts at 0 before first activation
        private bool _cramMode = false;
        private CramModeOptions? _cramOptions;

        private void ActivateNextBatch()
        {
            if (_cramMode)
            {
                _activeBatch.Clear();
                var now = DateTime.UtcNow;
                var cramBatch = _cards.Where(c => c.Stage == Stage.Seen && c.DueAt <= now)
                                       .Take(_batchSize)
                                       .ToList();
                _activeBatch.AddRange(cramBatch);

                if (_activeBatch.Count < _batchSize)
                {
                    var additional = _cards.Where(c => c.Stage == Stage.Avail)
                                            .Take(_batchSize - _activeBatch.Count)
                                            .ToList();
                    _activeBatch.AddRange(additional);
                }

                if (_activeBatch.Any())
                    _batchIndex++;
                _recentlyShown.Clear();
                return;
            }
            var nextBatch = _cards.Where(c => c.Stage == Stage.Avail).Take(_batchSize).ToList();
            if (nextBatch.Any())
            {
                _batchIndex++;
                _activeBatch = nextBatch;
                _recentlyShown.Clear();
            }
            else
            {
                _activeBatch.Clear();
            }
        }

        private const string PrefReviewStatePrefix = "ReviewState_";

        public SrsCard? CurrentCard { get; private set; }
        public int ReviewerId { get; private set; }
        public int CorrectCount { get; private set; }
        public int WrongCount { get; private set; }
        public bool SessionComplete { get; private set; }

        // scheduling params
        private readonly double MinEase = 1.3;
        private readonly double MaxEase = 3.0;
        private readonly double AgainEasePenalty = 0.2;
        private int _saveCounter = 0;
        private DateTime _lastSaveTime = DateTime.UtcNow;

        public int Total => _cards.Count;
        public int Seen => _cards.Count(c => c.SeenCount > 0);
        public int Learned => _learnedEver.Count;
        public int Skilled => _cards.Count(c => c.CountedSkilled);
        public int Memorized => _cards.Count(c => c.CountedMemorized);
        public int BatchIndex => _batchIndex;
        public int BatchSize => _batchSize;
        public int RemainingAvail => _cards.Count(c => c.Stage == Stage.Avail);

        public event Action? StateChanged;

        public async Task LoadCardsAsync(int reviewerId, Func<int, Task<IEnumerable<SrsCard>>> fetchFunc)
        {
            await _lock.WaitAsync();
            try
            {
                _cramMode = false;
                _batchSize = DEFAULT_BATCH_SIZE; // ensure default batch size restored
                ReviewerId = reviewerId;
                _cards.Clear();
                _learnedEver.Clear();
                CorrectCount = 0;
                WrongCount = 0;
                _recentlyShown.Clear();
                _activeBatch.Clear();

                var loaded = await fetchFunc(reviewerId);
                _cards.AddRange(loaded);
                RestoreProgress();

                ActivateNextBatch();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task LoadCardsForCramModeAsync(int reviewerId, Func<int, Task<IEnumerable<SrsCard>>> fetchFunc, CramModeOptions? options = null)
        {
            await _lock.WaitAsync();
            try
            {
                _cramMode = true;
                _cramOptions = options ?? CramModeOptions.Default;
                _batchSize = 5; // cram mode batch size override
                ReviewerId = reviewerId;

                _cards.Clear();
                _recentlyShown.Clear();
                _activeBatch.Clear();

                var loaded = await fetchFunc(reviewerId);
                _cards.AddRange(loaded);

                // Detect whether previous progress exists; if not, treat as fresh (reset learned/skilled/memorized stats)
                var payload = Preferences.Get(PrefReviewStatePrefix + reviewerId, null);
                _learnedEver.Clear();
                CorrectCount = 0;
                WrongCount = 0;
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    RestoreProgress(); // rehydrate sets if there was saved progress
                }

                var now = DateTime.UtcNow;
                foreach (var card in _cards)
                {
                    if (payload == null || card.Stage == Stage.Avail) // fresh cards or after reset
                        card.Stage = Stage.Seen;
                    card.DueAt = now;
                    card.Interval = _cramOptions.InitialInterval;
                    card.CooldownUntil = now;
                }

                ActivateNextBatch();
            }
            finally
            {
                _lock.Release();
            }
        }

        private void ApplyCooldown(SrsCard card, bool success)
        {
            var now = DateTime.UtcNow;
            if (_cramMode && _cramOptions != null)
            {
                card.CooldownUntil = now + TimeSpan.FromSeconds(success ? _cramOptions.CorrectCooldownSeconds : _cramOptions.FailCooldownSeconds);
            }
            else
            {
                card.CooldownUntil = now + TimeSpan.FromSeconds(success ? 3 : 10);
            }
            card.DueAt = now + card.Interval;
        }

        private void SaveProgressThrottled()
        {
            if (_saveCounter % 5 == 0 || (DateTime.UtcNow - _lastSaveTime).TotalSeconds > 15)
            {
                SaveProgress();
                _lastSaveTime = DateTime.UtcNow;
            }
        }

        public async Task GradeCardAsync(bool success)
        {
            await _lock.WaitAsync();
            try
            {
                if (CurrentCard == null) return;
                var card = CurrentCard;
                var now = DateTime.UtcNow;

                if (success)
                {
                    CorrectCount++;
                    card.ConsecutiveCorrects++;
                    card.Ease = Math.Clamp(card.Ease + 0.1, MinEase, MaxEase); // capped growth

                    if (card.Interval == TimeSpan.Zero)
                        card.Interval = TimeSpan.FromMinutes(1);
                    else
                        card.Interval = TimeSpan.FromDays(Math.Max(1, card.Interval.TotalDays * card.Ease));

                    card.Interval *= (Random.Shared.NextDouble() * 0.2 + 0.9);

                    if (card.Stage == Stage.Seen)
                    {
                        if (!card.CorrectOnce)
                            card.CorrectOnce = true;
                        else
                        {
                            card.Stage = Stage.Learned;
                            _learnedEver.Add(card);
                            card.CorrectOnce = false;
                        }
                    }
                    else if (card.Stage == Stage.Learned)
                    {
                        card.Stage = Stage.Skilled;
                        card.CountedSkilled = true;
                    }
                    else if (card.Stage == Stage.Skilled)
                    {
                        card.Stage = Stage.Memorized;
                        card.CountedMemorized = true;
                    }

                    if (_activeBatch.Count > 0 && _activeBatch.All(b => b.Stage >= Stage.Learned))
                    {
                        ActivateNextBatch();
                    }
                }
                else
                {
                    WrongCount++;
                    card.ConsecutiveCorrects = 0;
                    card.CorrectOnce = false;
                    bool wasAdvanced = card.Stage > Stage.Seen;
                    if (wasAdvanced)
                    {
                        _learnedEver.Remove(card);
                        card.CountedSkilled = false;
                        card.CountedMemorized = false;
                    }
                    card.Ease = Math.Clamp(card.Ease - AgainEasePenalty, MinEase, MaxEase); // bounded decay
                    card.Interval = TimeSpan.FromMinutes(1);
                    card.Stage = Stage.Seen;
                }

                // Single cooldown application
                ApplyCooldown(card, success);
                _saveCounter++;
                SaveProgressThrottled();
            }
            finally
            {
                _lock.Release();
                StateChanged?.Invoke();
            }
        }

        public async Task GradeCardForCramModeAsync(bool success)
        {
            await _lock.WaitAsync();
            try
            {
                if (!_cramMode) { await GradeCardAsync(success); return; }
                if (CurrentCard == null) return;
                var card = CurrentCard;
                var opts = _cramOptions ?? CramModeOptions.Default;

                if (success)
                {
                    CorrectCount++;
                    card.ConsecutiveCorrects++;
                    card.Ease = Math.Clamp(card.Ease + opts.EaseIncrement, MinEase, MaxEase);

                    if (card.Interval == TimeSpan.Zero)
                        card.Interval = opts.InitialInterval == TimeSpan.Zero ? TimeSpan.FromMinutes(1) : opts.InitialInterval;

                    // Growth multiplier allows faster or slower expansion
                    card.Interval = TimeSpan.FromMilliseconds(card.Interval.TotalMilliseconds * opts.IntervalGrowthMultiplier);

                    // Randomization window
                    var randFactor = opts.IntervalRandomLow + Random.Shared.NextDouble() * (opts.IntervalRandomHigh - opts.IntervalRandomLow);
                    card.Interval = TimeSpan.FromMilliseconds(card.Interval.TotalMilliseconds * randFactor);

                    if (card.Stage == Stage.Seen)
                    {
                        card.Stage = Stage.Learned;
                        _learnedEver.Add(card);
                    }
                    else if (card.Stage == Stage.Learned)
                    {
                        card.Stage = Stage.Skilled;
                        card.CountedSkilled = true;
                    }
                    else if (card.Stage == Stage.Skilled)
                    {
                        card.Stage = Stage.Memorized;
                        card.CountedMemorized = true;
                    }

                    if (_activeBatch.Count > 0 && _activeBatch.All(b => b.Stage >= Stage.Learned))
                        ActivateNextBatch();
                }
                else
                {
                    WrongCount++;
                    card.ConsecutiveCorrects = 0;
                    card.Ease = Math.Max(MinEase, card.Ease - AgainEasePenalty);
                    card.Interval = opts.OnFailInterval; // configurable reset interval
                    bool wasAdvanced = card.Stage > Stage.Seen;
                    if (wasAdvanced)
                    {
                        _learnedEver.Remove(card);
                        card.CountedSkilled = false;
                        card.CountedMemorized = false;
                    }
                    card.Stage = Stage.Seen;
                }

                ApplyCooldown(card, success);
                _saveCounter++;
                SaveProgressThrottled();
            }
            finally
            {
                _lock.Release();
                StateChanged?.Invoke();
            }
        }

        public async Task SkipCardAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (CurrentCard != null)
                    CurrentCard.DueAt = DateTime.UtcNow.AddMinutes(2);
            }
            finally
            {
                _lock.Release();
                PickNextCard();
            }
        }

        private bool ShouldMarkSessionComplete()
        {
            // Cram mode session completion only when all cards memorized
            return _cramMode && _cards.All(c => c.Stage == Stage.Memorized);
        }

        public void PickNextCard()
        {
            _lock.Wait();
            try
            {
                var now = DateTime.UtcNow;
                IEnumerable<SrsCard> candidatePool = _activeBatch.Count > 0 ? _activeBatch : _cards;
                var due = candidatePool
                    .Where(c => c.Stage != Stage.Avail && c.DueAt <= now && c.CooldownUntil <= now && !_recentlyShown.Contains(c))
                    .OrderBy(c => c.DueAt)
                    .ToList();
                var next = due.FirstOrDefault(c => !ReferenceEquals(c, CurrentCard))
                        ?? due.FirstOrDefault();
                if (next == null)
                {
                    var availInBatch = candidatePool.FirstOrDefault(c => c.Stage == Stage.Avail);
                    if (availInBatch != null)
                    {
                        availInBatch.Stage = Stage.Seen;
                        availInBatch.DueAt = now;
                        next = availInBatch;
                    }
                    else
                    {
                        if (_activeBatch.Count > 0 && _activeBatch.All(b => b.Stage >= Stage.Learned))
                        {
                            ActivateNextBatch();
                            candidatePool = _activeBatch.Count > 0 ? _activeBatch : _cards;
                        }
                        due = candidatePool
                            .Where(c => c.Stage != Stage.Avail && c.CooldownUntil <= now && !_recentlyShown.Contains(c))
                            .OrderBy(c => c.DueAt)
                            .ToList();
                        next = due.FirstOrDefault(c => !ReferenceEquals(c, CurrentCard))
                               ?? due.FirstOrDefault();
                        if (next == null)
                        {
                            var future = candidatePool
                                .Where(c => c.Stage != Stage.Avail && c.CooldownUntil <= now)
                                .OrderBy(c => c.DueAt)
                                .FirstOrDefault();
                            if (future != null)
                            {
                                if (future.DueAt > now) future.DueAt = now;
                                next = future;
                            }
                            else
                            {
                                var cooldown = candidatePool
                                    .Where(c => c.Stage != Stage.Avail)
                                    .OrderBy(c => c.CooldownUntil)
                                    .FirstOrDefault();
                                if (cooldown != null)
                                {
                                    cooldown.CooldownUntil = now;
                                    cooldown.DueAt = now;
                                    next = cooldown;
                                }
                            }
                        }
                    }
                }
                if (next == null)
                {
                    SessionComplete = ShouldMarkSessionComplete();
                    return;
                }
                CurrentCard = next;
                _recentlyShown.Enqueue(next);
                if (_recentlyShown.Count > RECENT_BUFFER_SIZE) _recentlyShown.Dequeue();
                CurrentCard.SeenCount++;
            }
            finally
            {
                _lock.Release();
                StateChanged?.Invoke();
            }
        }

        public void NextBatch()
        {
            _lock.Wait();
            try
            {
                ActivateNextBatch();
                if (!_cramMode)
                    SessionComplete = false;
                CurrentCard = null; // force fresh pick
            }
            finally
            {
                _lock.Release();
            }
            PickNextCard();
        }

        public void SaveProgress()
        {
            try
            {
                var payload = _cards.Select(c => new
                {
                    c.Id,
                    Stage = c.Stage.ToString(),
                    c.DueAt,
                    c.Ease,
                    IntervalDays = c.Interval.TotalDays,
                    c.CorrectOnce,
                    c.ConsecutiveCorrects,
                    c.CountedSkilled,
                    c.CountedMemorized,
                    c.SeenCount
                }).ToList();

                Preferences.Set(PrefReviewStatePrefix + ReviewerId, JsonSerializer.Serialize(payload));
            }
            catch { }
        }

        private void RestoreProgress()
        {
            try
            {
                var payload = Preferences.Get(PrefReviewStatePrefix + ReviewerId, null);
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
                    card.Ease = dto.GetProperty("Ease").GetDouble();
                    card.Interval = TimeSpan.FromDays(dto.GetProperty("IntervalDays").GetDouble());
                    card.CorrectOnce = dto.GetProperty("CorrectOnce").GetBoolean();
                    card.ConsecutiveCorrects = dto.GetProperty("ConsecutiveCorrects").GetInt32();
                    card.CountedSkilled = dto.GetProperty("CountedSkilled").GetBoolean();
                    card.CountedMemorized = dto.GetProperty("CountedMemorized").GetBoolean();
                    card.SeenCount = dto.TryGetProperty("SeenCount", out var sc) ? sc.GetInt32() : card.SeenCount;

                    // Rehydrate learned/skilled/memorized tracking sets based on stage
                    if (card.Stage >= Stage.Learned)
                    {
                        _learnedEver.Add(card);
                        if (card.Stage >= Stage.Skilled) card.CountedSkilled = true;
                        if (card.Stage == Stage.Memorized) card.CountedMemorized = true;
                    }
                }
            }
            catch { }
        }
    }
}
