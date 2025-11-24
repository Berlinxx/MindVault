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

    public string ProgressPercentText => $"{(int)(ProgressRatio * 100)}% {ProgressLabel}";
    public string DueText => $"{Due} due";
}
