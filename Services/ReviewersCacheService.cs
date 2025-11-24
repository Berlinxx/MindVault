using mindvault.Data;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;

namespace mindvault.Services;

public class ReviewersCacheService
{
    readonly DatabaseService _db;
    public bool IsLoaded { get; private set; }
    public List<ReviewerBasic> Items { get; } = new();

    const string LastPlayedKeyPrefix = "reviewer_last_played_";
    const string PrefReviewStatePrefix = "ReviewState_"; // matches CourseReviewPage
    const int MemorizedThreshold = 21;

    public ReviewersCacheService(DatabaseService db)
    {
        _db = db;
    }

    public async Task PreloadAsync()
    {
        if (IsLoaded) return;
        try
        {
            var reviewers = await _db.GetReviewersAsync();
            var stats = await _db.GetReviewerStatsAsync();
            var statsMap = stats.ToDictionary(s => s.ReviewerId);
            Items.Clear();
            foreach (var r in reviewers)
            {
                statsMap.TryGetValue(r.Id, out var s);
                int total = s?.Total ?? 0;
                int learned = s?.Learned ?? 0;
                var lastPlayed = Preferences.Get(LastPlayedKeyPrefix + r.Id, DateTime.MinValue);
                double progressRatio = total == 0 ? 0 : (double)learned / total;
                string progressLabel = "Learned";
                int due = 0;

                try
                {
                    var payload = Preferences.Get(PrefReviewStatePrefix + r.Id, null);
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        var dtos = JsonSerializer.Deserialize<List<CardStateDto>>(payload);
                        if (dtos != null && total > 0)
                        {
                            int memorized = 0, skilled = 0, learnedAdv = 0, dueCnt = 0;
                            var now = DateTime.UtcNow;
                            foreach (var d in dtos)
                            {
                                var dueAt = new DateTime(d.DueAtTicks, DateTimeKind.Utc);
                                if (!string.Equals(d.Stage, "Avail", System.StringComparison.OrdinalIgnoreCase) && dueAt <= now)
                                    dueCnt++;
                                if (d.CountedMemorized || d.ReviewSuccessStreak >= MemorizedThreshold)
                                    memorized++;
                                else if (d.CountedSkilled || d.ReviewSuccessStreak >= 1)
                                    skilled++;
                                else if (d.InReview || string.Equals(d.Stage, "Learned", System.StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(d.Stage, "Skilled", System.StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(d.Stage, "Memorized", System.StringComparison.OrdinalIgnoreCase))
                                    learnedAdv++;
                            }
                            double ratio; string label;
                            if (memorized > 0) { ratio = (double)memorized / total; label = "Memorized"; }
                            else if (skilled > 0) { ratio = (double)skilled / total; label = "Skilled"; }
                            else { ratio = (double)learnedAdv / total; label = "Learned"; }
                            progressRatio = ratio;
                            progressLabel = label;
                            due = dueCnt;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine($"[ReviewersCache] Refinement failed for reviewer {r.Id}: {ex.Message}");
                }

                Items.Add(new ReviewerBasic
                {
                    Id = r.Id,
                    Title = r.Title,
                    Questions = total,
                    LearnedRatio = total == 0 ? 0 : (double)learned / total,
                    CreatedUtc = r.CreatedUtc,
                    LastPlayedUtc = lastPlayed == DateTime.MinValue ? null : lastPlayed,
                    ProgressRatio = progressRatio,
                    ProgressLabel = progressLabel,
                    Due = due
                });
            }
            IsLoaded = true;
        }
        catch
        {
            // swallow – page will fallback to normal load
        }
    }

    public async Task RefreshAsync()
    {
        try
        {
            var reviewers = await _db.GetReviewersAsync();
            var stats = await _db.GetReviewerStatsAsync();
            var statsMap = stats.ToDictionary(s => s.ReviewerId);
            Items.Clear();
            foreach (var r in reviewers)
            {
                statsMap.TryGetValue(r.Id, out var s);
                int total = s?.Total ?? 0;
                int learned = s?.Learned ?? 0;
                var lastPlayed = Preferences.Get(LastPlayedKeyPrefix + r.Id, DateTime.MinValue);
                double progressRatio = total == 0 ? 0 : (double)learned / total; // baseline learned
                string progressLabel = "Learned";
                int due = 0;
                try
                {
                    var payload = Preferences.Get(PrefReviewStatePrefix + r.Id, null);
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        var dtos = JsonSerializer.Deserialize<List<CardStateDto>>(payload);
                        if (dtos != null && total > 0)
                        {
                            int memorized = 0, skilled = 0, learnedAdv = 0, dueCnt = 0;
                            var now = DateTime.UtcNow;
                            foreach (var d in dtos)
                            {
                                var dueAt = new DateTime(d.DueAtTicks, DateTimeKind.Utc);
                                if (!string.Equals(d.Stage, "Avail", System.StringComparison.OrdinalIgnoreCase) && dueAt <= now)
                                    dueCnt++;
                                if (d.CountedMemorized || d.ReviewSuccessStreak >= MemorizedThreshold)
                                    memorized++;
                                else if (d.CountedSkilled || d.ReviewSuccessStreak >= 1)
                                    skilled++;
                                else if (d.InReview || string.Equals(d.Stage, "Learned", System.StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(d.Stage, "Skilled", System.StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(d.Stage, "Memorized", System.StringComparison.OrdinalIgnoreCase))
                                    learnedAdv++;
                            }
                            double ratio; string label;
                            if (memorized > 0) { ratio = (double)memorized / total; label = "Memorized"; }
                            else if (skilled > 0) { ratio = (double)skilled / total; label = "Skilled"; }
                            else { ratio = (double)learnedAdv / total; label = "Learned"; }
                            progressRatio = ratio;
                            progressLabel = label;
                            due = dueCnt;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine($"[ReviewersCache] Refresh refinement failed for reviewer {r.Id}: {ex.Message}");
                }
                Items.Add(new ReviewerBasic
                {
                    Id = r.Id,
                    Title = r.Title,
                    Questions = total,
                    LearnedRatio = total == 0 ? 0 : (double)learned / total,
                    CreatedUtc = r.CreatedUtc,
                    LastPlayedUtc = lastPlayed == DateTime.MinValue ? null : lastPlayed,
                    ProgressRatio = progressRatio,
                    ProgressLabel = progressLabel,
                    Due = due
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewersCache] RefreshAsync failed: {ex.Message}");
        }
    }

    // Minimal DTO copied from ReviewersPage for refinement
    record CardStateDto(
        int Id,
        string Stage,
        long DueAtTicks,
        bool InReview,
        int ReviewSuccessStreak,
        bool CountedSkilled,
        bool CountedMemorized
    );
}

public class ReviewerBasic
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Questions { get; set; }
    public double LearnedRatio { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastPlayedUtc { get; set; }

    // New refined fields for instant page load
    public double ProgressRatio { get; set; }
    public string ProgressLabel { get; set; } = "Learned";
    public int Due { get; set; }
}