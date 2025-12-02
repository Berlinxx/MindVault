namespace mindvault.Pages;

public class ObservableRangeCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
{
    public void ReplaceRange(System.Collections.Generic.IEnumerable<T> items)
    {
        if (items == null) return;
        Items.Clear();
        foreach (var i in items) Items.Add(i);
        OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
    }
}

public class ReviewerCard : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    void Notify(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Questions { get; set; }

    double _progressRatio;
    public double ProgressRatio { get => _progressRatio; set { if (_progressRatio == value) return; _progressRatio = value; Notify(nameof(ProgressRatio)); Notify(nameof(ProgressPercentText)); } }

    string _progressLabel = "Learned";
    public string ProgressLabel { get => _progressLabel; set { if (_progressLabel == value) return; _progressLabel = value; Notify(nameof(ProgressLabel)); Notify(nameof(ProgressPercentText)); } }

    int _due;
    public int Due { get => _due; set { if (_due == value) return; _due = value; Notify(nameof(Due)); Notify(nameof(DueText)); } }

    public DateTime CreatedUtc { get; set; }
    public DateTime? LastPlayedUtc { get; set; }

    // Internal mastery counts - used to calculate progressive milestone
    internal int LearnedCount { get; set; }
    internal int SkilledCount { get; set; }
    internal int MemorizedCount { get; set; }

    public string ProgressPercentText => $"{(int)(ProgressRatio * 100)}% {ProgressLabel}";
    public string DueText => $"{Due} due";

    /// <summary>
    /// Calculates progress based on progressive milestones:
    /// - If not all cards are Learned ? track progress to Learned
    /// - If all Learned but not all Skilled ? track progress to Skilled
    /// - If all Skilled but not all Memorized ? track progress to Memorized
    /// - If all Memorized ? show 100% Memorized
    /// </summary>
    public void CalculateProgressiveMilestone()
    {
        if (Questions == 0)
        {
            ProgressRatio = 0;
            ProgressLabel = "Learned";
            return;
        }

        // Check if all cards reached each milestone
        bool allLearned = LearnedCount >= Questions;
        bool allSkilled = SkilledCount >= Questions;
        bool allMemorized = MemorizedCount >= Questions;

        if (!allLearned)
        {
            // Still working on Learned milestone
            ProgressRatio = (double)LearnedCount / Questions;
            ProgressLabel = "Learned";
        }
        else if (!allSkilled)
        {
            // All Learned, now working on Skilled milestone
            ProgressRatio = (double)SkilledCount / Questions;
            ProgressLabel = "Skilled";
        }
        else if (!allMemorized)
        {
            // All Skilled, now working on Memorized milestone
            ProgressRatio = (double)MemorizedCount / Questions;
            ProgressLabel = "Memorized";
        }
        else
        {
            // All cards are Memorized!
            ProgressRatio = 1.0;
            ProgressLabel = "Memorized";
        }
    }
}
