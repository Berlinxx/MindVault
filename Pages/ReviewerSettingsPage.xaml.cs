using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using mindvault.Services;
using mindvault.Utils;
using System.Diagnostics;
using Microsoft.Maui.Storage;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using mindvault.Utils.Messages;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace mindvault.Pages;

[QueryProperty(nameof(ReviewerId), "reviewerId")]
[QueryProperty(nameof(ReviewerTitle), "reviewerTitle")]
public partial class ReviewerSettingsPage : ContentPage, INotifyPropertyChanged
{
    int _reviewerId;
    string _reviewerTitle = string.Empty;

    public int ReviewerId
    {
        get => _reviewerId;
        set { if (_reviewerId == value) return; _reviewerId = value; OnPropertyChanged(); }
    }

    public string ReviewerTitle
    {
        get => _reviewerTitle;
        set { if (_reviewerTitle == value) return; _reviewerTitle = value ?? string.Empty; OnPropertyChanged(); }
    }

    const string PrefRoundSize = "RoundSize"; // base key
    const string PrefStudyMode = "StudyMode"; // base key
    const string PrefAutoTts = "AutoTts"; // base key for auto text-to-speech
    const string PrefReviewStatePrefix = "ReviewState_"; // matches CourseReviewPage

    int _roundSize;
    string _mode = "Default";
    bool _autoTts;

    public ReviewerSettingsPage() : this("Math Reviewer") { }

    public ReviewerSettingsPage(string reviewerTitle)
    {
        InitializeComponent();
        ReviewerTitle = reviewerTitle ?? string.Empty;
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this);
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (ReviewerId <= 0 && !string.IsNullOrWhiteSpace(ReviewerTitle))
        {
            try
            {
                var db = ServiceHelper.GetRequiredService<DatabaseService>();
                var reviewers = await db.GetReviewersAsync();
                ReviewerId = reviewers.FirstOrDefault(r => r.Title == ReviewerTitle)?.Id ?? 0;
            }
            catch { }
        }
        // Load per-deck settings
        _roundSize = Preferences.Get(DeckKey(PrefRoundSize, ReviewerId), Preferences.Get(PrefRoundSize, 10));
        _mode = Preferences.Get(DeckKey(PrefStudyMode, ReviewerId), Preferences.Get(PrefStudyMode, "Default"));
        _autoTts = Preferences.Get(DeckKey(PrefAutoTts, ReviewerId), Preferences.Get(PrefAutoTts, false));
        BindingContext = this; // ensure latest props are bound
        UpdateChipUI();
        UpdateModeUI(_mode);
        UpdateAutoTtsUI();
    }

    static string DeckKey(string key, int id) => id > 0 ? $"{key}_{id}" : key;

    void UpdateChipUI()
    {
        var chipStyle = (Style)Resources["Chip"]; var selStyle = (Style)Resources["ChipSelected"];
        var chip10 = this.FindByName<Border>("Round10Chip");
        var chip20 = this.FindByName<Border>("Round20Chip");
        var chip30 = this.FindByName<Border>("Round30Chip");
        var chip40 = this.FindByName<Border>("Round40Chip");
        if (chip10 != null) chip10.Style = chipStyle;
        if (chip20 != null) chip20.Style = chipStyle;
        if (chip30 != null) chip30.Style = chipStyle;
        if (chip40 != null) chip40.Style = chipStyle;
        Border? sel = _roundSize switch
        {
            10 => chip10,
            20 => chip20,
            30 => chip30,
            40 => chip40,
            _ => null
        };
        if (sel != null) sel.Style = selStyle;
    }

    void UpdateModeUI(string mode)
    {
        var defaultTile = this.FindByName<Border>("DefaultTile");
        var examTile = this.FindByName<Border>("ExamTile");
        if (defaultTile == null || examTile == null) return;
        if (mode == "Exam")
        {
            defaultTile.BackgroundColor = Colors.LightGray;
            examTile.BackgroundColor = (Color)Resources["Primary"]; // highlight exam
            var examLabel = this.FindByName<Label>("ExamTileLabel");
            if (examLabel != null) examLabel.TextColor = Colors.White;
            var defLabel = this.FindByName<Label>("DefaultTileLabel");
            if (defLabel != null) defLabel.TextColor = Colors.Black;
        }
        else
        {
            defaultTile.BackgroundColor = (Color)Resources["Primary"]; // highlight default
            examTile.BackgroundColor = Colors.LightGray;
            var defLabel = this.FindByName<Label>("DefaultTileLabel");
            if (defLabel != null) defLabel.TextColor = Colors.White;
            var examLabel = this.FindByName<Label>("ExamTileLabel");
            if (examLabel != null) examLabel.TextColor = Colors.Black;
        }
    }

    void UpdateAutoTtsUI()
    {
        var autoTtsSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("AutoTtsSwitch");
        if (autoTtsSwitch != null)
        {
            autoTtsSwitch.IsToggled = _autoTts;
        }
    }

    private void OnAutoTtsToggled(object? sender, ToggledEventArgs e)
    {
        _autoTts = e.Value;
        Preferences.Set(DeckKey(PrefAutoTts, ReviewerId), _autoTts);
        // Send message to notify CourseReviewPage of the change
        WeakReferenceMessenger.Default.Send(new AutoTtsChangedMessage(ReviewerId, _autoTts));
    }

    private void OnDefaultModeTapped(object? sender, TappedEventArgs e)
    {
        if (_mode == "Default") return; // no change
        _mode = "Default";
        Preferences.Set(DeckKey(PrefStudyMode, ReviewerId), _mode);
        UpdateModeUI(_mode);
        WeakReferenceMessenger.Default.Send(new StudyModeChangedMessage(ReviewerId, _mode));
    }

    private void OnExamModeTapped(object? sender, TappedEventArgs e)
    {
        if (_mode == "Exam") return; // no change
        _mode = "Exam";
        Preferences.Set(DeckKey(PrefStudyMode, ReviewerId), _mode);
        UpdateModeUI(_mode);
        WeakReferenceMessenger.Default.Send(new StudyModeChangedMessage(ReviewerId, _mode));
    }

    private void OnRoundSizeTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string s && int.TryParse(s, out var n))
        {
            if (n == _roundSize) return; // no change
            _roundSize = n;
            Preferences.Set(DeckKey(PrefRoundSize, ReviewerId), _roundSize);
            UpdateChipUI();
            WeakReferenceMessenger.Default.Send(new RoundSizeChangedMessage(ReviewerId, _roundSize));
        }
    }

    private async void OnResetProgressTapped(object? sender, TappedEventArgs e)
    {
        var confirmResult = await this.ShowPopupAsync(new AppModal("Reset Progress", "This will erase your review progress for this course. Continue?", "Reset", "Cancel"));
        var confirm = confirmResult is bool b && b;
        if (!confirm) return;

        try
        {
            int reviewerId = ReviewerId;
            if (reviewerId <= 0 && !string.IsNullOrWhiteSpace(ReviewerTitle))
            {
                var db = ServiceHelper.GetRequiredService<DatabaseService>();
                var reviewers = await db.GetReviewersAsync();
                reviewerId = reviewers.FirstOrDefault(r => r.Title == ReviewerTitle)?.Id ?? 0;
            }
            if (reviewerId > 0)
            {
                // Remove legacy Preferences storage
                Preferences.Remove(PrefReviewStatePrefix + reviewerId);
                
                // Remove file-based progress storage
                var progressFilePath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "Progress", $"ReviewState_{reviewerId}.json");
                if (System.IO.File.Exists(progressFilePath))
                {
                    System.IO.File.Delete(progressFilePath);
                    Debug.WriteLine($"[ReviewerSettings] Deleted progress file: {progressFilePath}");
                }
                
                WeakReferenceMessenger.Default.Send(new ProgressResetMessage(reviewerId));
                this.ShowPopup(new AppModal("Progress Reset", "Your review progress has been cleared.", "OK"));
            }
            else
            {
                this.ShowPopup(new AppModal("Not Found", "Could not resolve the current course.", "OK"));
            }
        }
        catch (Exception ex)
        {
            this.ShowPopup(new AppModal("Reset Failed", ex.Message, "OK"));
        }
    }

    private async void OnCloseTapped(object? sender, EventArgs e)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new SettingsClosedMessage(ReviewerId));
            await Navigator.PopAsync(Navigation);
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(new SettingsClosedMessage(ReviewerId));
            await Navigator.GoToAsync("///ReviewersPage");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mindvault.Utils.AnimHelpers.SlideFadeInAsync(Content);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
