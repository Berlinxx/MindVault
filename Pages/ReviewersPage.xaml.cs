using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Controls; // ensure ContentPage & InitializeComponent
using mindvault.Services;
using mindvault.Utils;

namespace mindvault.Pages;

public partial class ReviewersPage : ContentPage
{
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "All (Default)",
        "Last Played (Recent first)",
        "Alphabetical (A–Z)",
        "Alphabetical (Z–A)",
        "Created Date (Newest first)",
        "Created Date (Oldest first)"
    };

    private string _selectedSort = "Last Played (Recent first)";
    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (_selectedSort == value) return;
            _selectedSort = value ?? "All (Default)";
            OnPropertyChanged(nameof(SelectedSort));
            ApplySort();
        }
    }

    bool _isSearchVisible;
    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set { if (_isSearchVisible == value) return; _isSearchVisible = value; OnPropertyChanged(); }
    }

    System.Threading.CancellationTokenSource? _searchDebounce;
    string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? string.Empty;
            OnPropertyChanged();
            _searchDebounce?.Cancel();
            var cts = new System.Threading.CancellationTokenSource();
            _searchDebounce = cts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250, cts.Token);
                    if (cts.IsCancellationRequested) return;
                    MainThread.BeginInvokeOnMainThread(ApplySort);
                }
                catch (TaskCanceledException) { }
            });
        }
    }

    public ObservableRangeCollection<ReviewerCard> Reviewers { get; } = new();
    public bool IsLoadingReviewers { get; set; }
    public bool IsLoaded { get; set; }

    private List<ReviewerCard> _baseline = new();
    readonly DatabaseService _db;

    public ReviewersPage()
    {
        InitializeComponent();
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this);
        _db = ServiceHelper.GetRequiredService<DatabaseService>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = StartEntryAnimationAsync();
        var cache = ServiceHelper.GetRequiredService<ReviewersCacheService>();
        _ = Task.Run(async () =>
        {
            await cache.RefreshAsync();
            var list = cache.Items.Select(item => new ReviewerCard
            {
                Id = item.Id,
                Title = item.Title,
                Questions = item.Questions,
                ProgressRatio = item.ProgressRatio,
                ProgressLabel = item.ProgressLabel,
                Due = item.Due,
                CreatedUtc = item.CreatedUtc,
                LastPlayedUtc = item.LastPlayedUtc
            }).ToList();
            _baseline = list;
            MainThread.BeginInvokeOnMainThread(ApplyLoadedData);
        });
        WireOnce();
    }

    async Task StartEntryAnimationAsync()
    {
        try
        {
            var target = this.FindByName<Grid>("RootGrid") ?? (VisualElement)Content;
            await AnimHelpers.SlideFadeInAsync(target);
        }
        catch { }
    }

    async Task LoadFromDbAsync()
    {
        var rows = await _db.GetReviewersAsync();
        var statsList = await _db.GetReviewerStatsAsync();
        var statsMap = statsList.ToDictionary(s => s.ReviewerId);
        var newBaseline = new List<ReviewerCard>();
        foreach (var r in rows)
        {
            statsMap.TryGetValue(r.Id, out var stats);
            int total = stats?.Total ?? 0;
            int learnedRaw = stats?.Learned ?? 0;
            var lastPlayed = Preferences.Get(GetLastPlayedKey(r.Id), DateTime.MinValue);
            double progressRatio = (total == 0) ? 0 : (double)learnedRaw / total;
            newBaseline.Add(new ReviewerCard
            {
                Id = r.Id,
                Title = r.Title,
                Questions = total,
                ProgressRatio = progressRatio,
                ProgressLabel = "Learned",
                Due = 0,
                CreatedUtc = r.CreatedUtc,
                LastPlayedUtc = lastPlayed == DateTime.MinValue ? null : lastPlayed
            });
        }
        _baseline = newBaseline;
    }

    void ApplyLoadedData()
    {
        Reviewers.ReplaceRange(_baseline);
        ApplySort();
        IsLoaded = true;
        IsLoadingReviewers = false;
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(IsLoadingReviewers));
    }

    static string GetLastPlayedKey(int reviewerId) => $"reviewer_last_played_{reviewerId}";

    bool _wired;
    void WireOnce()
    {
        if (_wired) return;
        _wired = true;
    }

    void ApplySort()
    {
        IEnumerable<ReviewerCard> source = _baseline;
        var keyword = SearchText?.Trim();
        if (!string.IsNullOrEmpty(keyword))
            source = source.Where(c => c.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);

        switch (SelectedSort)
        {
            case "Last Played (Recent first)":
                source = source.OrderByDescending(c => c.LastPlayedUtc.HasValue).ThenByDescending(c => c.LastPlayedUtc);
                break;
            case "Alphabetical (A–Z)":
                source = source.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase);
                break;
            case "Alphabetical (Z–A)":
                source = source.OrderByDescending(c => c.Title, StringComparer.OrdinalIgnoreCase);
                break;
            case "Created Date (Newest first)":
                source = source.OrderByDescending(c => c.CreatedUtc);
                break;
            case "Created Date (Oldest first)":
                source = source.OrderBy(c => c.CreatedUtc);
                break;
            case "All (Default)":
            default:
                break;
        }
        Reviewers.ReplaceRange(source.ToList());
    }

    private async void OnDeleteTapped(object? sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ReviewerCard reviewer)
        {
            bool confirmed = await PageHelpers.SafeDisplayAlertAsync(this, "Delete Reviewer",
                $"Are you sure you want to delete '{reviewer.Title}'?",
                "Delete", "Cancel");
            if (confirmed)
            {
                await _db.DeleteReviewerCascadeAsync(reviewer.Id);
                _baseline.RemoveAll(x => x.Id == reviewer.Id);
                ApplySort();
                await PageHelpers.SafeDisplayAlertAsync(this, "Deleted", $"'{reviewer.Title}' has been removed.", "OK");
            }
        }
    }

    private async void OnViewCourseTapped(object? sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ReviewerCard reviewer)
        {
            Debug.WriteLine($"[ReviewersPage] OpenCourse() -> CourseReviewPage");
            await Navigator.PushAsync(new CourseReviewPage(reviewer.Id, reviewer.Title), Navigation);
        }
    }

    private async void OnEditTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element el || el.BindingContext is not ReviewerCard reviewer) return;
        Debug.WriteLine($"[ReviewersPage] OpenEditor() -> ReviewerEditorPage (Id={reviewer.Id}, Title={reviewer.Title})");
        var route = $"///{nameof(ReviewerEditorPage)}?id={reviewer.Id}&title={Uri.EscapeDataString(reviewer.Title)}";
        await PageHelpers.SafeNavigateAsync(this, async () => await Shell.Current.GoToAsync(route),
            "Could not open editor");
    }

    private void OnSearchTapped(object? sender, TappedEventArgs e)
    {
        IsSearchVisible = !IsSearchVisible;
        if (IsSearchVisible)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Delay(50);
                var search = this.FindByName<SearchBar>("DeckSearchBar");
                search?.Focus();
            });
        }
        else
        {
            SearchText = string.Empty;
        }
    }

    private async void OnCreateReviewerTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("///TitleReviewerPage");
        }
        catch
        {
            await NavigationService.OpenTitle();
        }
    }

    private async void OnExportTapped(object? sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ReviewerCard reviewer)
        {
            try
            {
                var cards = await _db.GetFlashcardsAsync(reviewer.Id);
                var list = cards.Select(c => (c.Question, c.Answer)).ToList();
                await Navigator.PushAsync(new ExportPage(reviewer.Title, list), Navigation);
            }
            catch (Exception ex)
            {
                await PageHelpers.SafeDisplayAlertAsync(this, "Export", ex.Message, "OK");
            }
        }
    }

    private async void OnImportTapped(object? sender, EventArgs e)
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "text/plain" } },
                { DevicePlatform.iOS, new[] { "public.plain-text" } },
                { DevicePlatform.MacCatalyst, new[] { "public.plain-text" } },
                { DevicePlatform.WinUI, new[] { ".txt" } },
            });

            var pick = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select .txt export file",
                FileTypes = fileTypes
            });
            if (pick is null) return;
            if (!string.Equals(Path.GetExtension(pick.FileName), ".txt", System.StringComparison.OrdinalIgnoreCase))
            {
                await PageHelpers.SafeDisplayAlertAsync(this, "Import", "Only .txt files are supported.", "OK");
                return;
            }

            string content;
            using (var stream = await pick.OpenReadAsync())
            using (var reader = new StreamReader(stream))
                content = await reader.ReadToEndAsync();

            var (title, cards) = ParseExport(content);
            if (cards.Count == 0)
            {
                await PageHelpers.SafeDisplayAlertAsync(this, "Import", "No cards found in file.", "OK");
                return;
            }

            await Navigator.PushAsync(new ImportPage(title, cards), Navigation);
        }
        catch (Exception ex)
        {
            await PageHelpers.SafeDisplayAlertAsync(this, "Import Failed", ex.Message, "OK");
        }
    }

    private (string Title, List<(string Q, string A)> Cards) ParseExport(string content)
    {
        var lines = content.Replace("\r", string.Empty).Split('\n');
        string title = lines.FirstOrDefault(l => l.StartsWith("Reviewer:", StringComparison.OrdinalIgnoreCase))?.Substring(9).Trim() ?? "Imported Reviewer";
        var cards = new List<(string Q, string A)>();
        string? q = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("Q:", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(q)) { cards.Add((q, string.Empty)); }
                q = line.Substring(2).Trim();
            }
            else if (line.StartsWith("A:", StringComparison.OrdinalIgnoreCase))
            {
                var a = line.Substring(2).Trim();
                if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                {
                    cards.Add((q ?? string.Empty, a));
                    q = null;
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(q)) cards.Add((q, string.Empty));
        return (title, cards);
    }
}
