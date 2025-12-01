using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using Microsoft.Maui.Controls;
using System;
using mindvault.Utils;
using mindvault.Srs;
using mindvault.Services;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace mindvault.Pages;

public partial class SessionSummaryPage : ContentPage
{
    public int ReviewerId { get; }
    public string ReviewerTitle { get; }
    public int Correct { get; }
    public int Wrong { get; }
    public TimeSpan Duration { get; }
    public string DurationText => $"Duration: {Duration.Minutes}m {Duration.Seconds}s";
    public string MemorizedEstimateText { get; }
    public ObservableCollection<MistakeItem> Mistakes { get; }
    public bool HasMistakes => Mistakes.Count > 0;

    readonly SrsEngine _engineRef;
    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();

    public SessionSummaryPage(int reviewerId, string reviewerTitle, int correct, int wrong, TimeSpan duration, SrsEngine engineSnapshot, IReadOnlyList<(string Q,string A)> mistakes)
    {
        InitializeComponent();
        ReviewerId = reviewerId;
        ReviewerTitle = reviewerTitle;
        Correct = correct;
        Wrong = wrong;
        Duration = duration;
        _engineRef = engineSnapshot;
        MemorizedEstimateText = BuildMemorizedEstimate(engineSnapshot);
        Mistakes = new ObservableCollection<MistakeItem>(mistakes.Select(m => new MistakeItem { Q = m.Q, A = m.A }));
        BindingContext = this;
    }

    string BuildMemorizedEstimate(SrsEngine engine)
    {
        try
        {
            int total = engine.Total;
            int memorized = engine.Memorized;
            int skilled = engine.Skilled;
            int learned = engine.Learned;
            int remaining = Math.Max(0, total - memorized);
            if (remaining == 0) return "All cards already memorized.";
            double minutes = Math.Max(1, Duration.TotalMinutes);
            double correctPerMinute = Correct / minutes;
            if (correctPerMinute < 0.1) return "Need more data for estimate.";
            int pendingLearned = Math.Max(0, total - learned);
            int pendingSkilled = Math.Max(0, total - skilled);
            int pendingMemorized = Math.Max(0, total - memorized);
            int effortUnits = pendingLearned * 2 + pendingSkilled + pendingMemorized;
            double minutesNeeded = effortUnits / Math.Max(0.5, correctPerMinute);
            var est = TimeSpan.FromMinutes(minutesNeeded);
            return est.TotalHours >= 1
                ? $"Est. time to full memorization: ~{(int)est.TotalHours}h {est.Minutes}m"
                : $"Est. time to full memorization: ~{est.Minutes}m";
        }
        catch { return "Estimation unavailable."; }
    }

    async void OnReviewMistakes(object? sender, EventArgs e)
    {
        if (HasMistakes)
        {
            await Navigator.PushAsync(new ReviewMistakesPage(Mistakes.Select(m => (m.Q, m.A)).ToList()), Navigation);
        }
        else
        {
            this.ShowPopup(new AppModal("No Mistakes", "You didn't make any mistakes this round!", "OK"));
        }
    }
    async void OnStudyMore(object? sender, EventArgs e) => await Navigation.PopAsync();
    async void OnBack(object? sender, EventArgs e) => await NavigationService.CloseCourseToReviewers();
}

public class MistakeItem
{
    public string Q { get; set; } = string.Empty;
    public string A { get; set; } = string.Empty;
}
