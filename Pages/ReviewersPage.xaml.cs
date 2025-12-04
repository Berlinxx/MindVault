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
using mindvault.Controls;
using mindvault.Data;
using CommunityToolkit.Maui.Views;

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
    readonly GlobalDeckPreloadService _preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();
    
    // Guards to prevent rapid clicking
    private bool _isExporting = false;
    private bool _isImporting = false;

    public ReviewersPage()
    {
        InitializeComponent();
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this);
        _db = ServiceHelper.GetRequiredService<DatabaseService>();
    }

    private void OnDropdownTapped(object? sender, EventArgs e)
    {
        if (DropdownList != null)
        {
            DropdownList.IsVisible = !DropdownList.IsVisible;
        }
    }

    private void OnDropdownItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string selectedOption)
        {
            SelectedSort = selectedOption;
            if (DropdownList != null)
            {
                DropdownList.IsVisible = false;
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Handle Android back button to go to previous page instead of home
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
            else
            {
                // If this is the root page, go to HomePage
                await Shell.Current.GoToAsync("///HomePage");
            }
        });
        return true; // Prevent default back button behavior
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ShowLoadingAsync();

        // Build list after ensuring RAM decks are ready
        _ = Task.Run(async () =>
        {
            try
            {
                // Force refresh from database to pick up newly imported decks
                await _preloader.PreloadAllAsync(forceReload: true);

                var reviewers = await _db.GetReviewersAsync();
                var statsList = await _db.GetReviewerStatsAsync();
                var statsMap = statsList.ToDictionary(s => s.ReviewerId);

                var list = new List<ReviewerCard>();
                foreach (var r in reviewers)
                {
                    var total = _preloader.Decks.TryGetValue(r.Id, out var cards) ? cards.Count : 0;
                    
                    // Load SRS progress to get actual mastery counts
                    var (learnedCount, skilledCount, memorizedCount) = LoadSrsMasteryCounts(r.Id, cards);
                    
                    // Calculate how many cards are currently due
                    var dueCount = CalculateDueCount(r.Id, cards);
                    
                    var card = new ReviewerCard
                    {
                        Id = r.Id,
                        Title = r.Title,
                        Questions = total,
                        LearnedCount = learnedCount,
                        SkilledCount = skilledCount,
                        MemorizedCount = memorizedCount,
                        Due = dueCount,
                        CreatedUtc = r.CreatedUtc,
                        LastPlayedUtc = null
                    };
                    
                    // Calculate progressive milestone (Learned → Skilled → Memorized)
                    card.CalculateProgressiveMilestone();
                    
                    list.Add(card);
                }
                _baseline = list;
            }
            catch { }
            finally
            {
                MainThread.BeginInvokeOnMainThread(async () => await ApplyLoadedDataAsync());
            }
        });

        WireOnce();
    }

    private async Task ShowLoadingAsync()
    {
        IsLoadingReviewers = true;
        OnPropertyChanged(nameof(IsLoadingReviewers));
        var overlay = this.FindByName<Grid>("LoadingOverlay");
        if (overlay != null)
        {
            overlay.Opacity = 0;
            overlay.IsVisible = true;
            await overlay.FadeTo(1, 250, Easing.CubicInOut);
        }
    }

    private async Task HideLoadingAsync()
    {
        var overlay = this.FindByName<Grid>("LoadingOverlay");
        if (overlay == null) { IsLoadingReviewers = false; OnPropertyChanged(nameof(IsLoadingReviewers)); return; }
        await overlay.FadeTo(0, 300, Easing.CubicInOut);
        overlay.IsVisible = false;
        IsLoadingReviewers = false;
        OnPropertyChanged(nameof(IsLoadingReviewers));
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

    private async Task ApplyLoadedDataAsync()
    {
        Reviewers.ReplaceRange(_baseline);
        ApplySort();
        IsLoaded = true;
        OnPropertyChanged(nameof(IsLoaded));

        await HideLoadingAsync();

        try { await AnimHelpers.SlideFadeInAsync(this.FindByName<Grid>("RootGrid") ?? (VisualElement)Content); } catch { }

        var listView = this.FindByName<CollectionView>("ReviewerListView");
        if (listView != null)
        {
            listView.Opacity = 0;
            await listView.FadeTo(1, 350, Easing.CubicInOut);
        }
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
            var confirmResult = await this.ShowPopupAsync(new AppModal("Delete Reviewer",
                $"Are you sure you want to delete '{reviewer.Title}'?",
                "Delete", "Cancel"));
            bool confirmed = confirmResult is bool b && b;
            if (confirmed)
            {
                await _db.DeleteReviewerCascadeAsync(reviewer.Id);
                _baseline.RemoveAll(x => x.Id == reviewer.Id);
                ApplySort();
                await this.ShowPopupAsync(new AppModal("Deleted", $"'{reviewer.Title}' has been removed.", "OK"));
            }
        }
    }

    private async void OnViewCourseTapped(object? sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ReviewerCard reviewer)
        {
            Debug.WriteLine($"[ReviewersPage] OpenCourse() -> CourseReviewPage");
            await PageHelpers.SafeNavigateAsync(this, async () =>
            {
                await Navigator.PushAsync(new CourseReviewPage(reviewer.Id, reviewer.Title), Navigation);
            }, "Could not open course");
        }
    }

    private async void OnEditTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element el || el.BindingContext is not ReviewerCard reviewer)
            return;

        Debug.WriteLine($"[ReviewersPage] OpenEditor() -> ReviewerEditorPage (Id={reviewer.Id}, Title={reviewer.Title})");

        // Show spinner immediately
        await ShowLoadingAsync();

        var route = $"///{nameof(ReviewerEditorPage)}?id={reviewer.Id}&title={Uri.EscapeDataString(reviewer.Title)}";

        await PageHelpers.SafeNavigateAsync(this, async () =>
        {
            await Shell.Current.GoToAsync(route);
        }, "Could not open editor");
        // Do NOT hide spinner here; it will hide when returning to this page.
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
        // Prevent multiple simultaneous exports
        if (_isExporting) return;
        _isExporting = true;
        
        try
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
        finally
        {
            // Add small delay before allowing next export
            await Task.Delay(300);
            _isExporting = false;
        }
    }

    private async void OnImportTapped(object? sender, EventArgs e)
    {
        await PerformImportAsync(this, Navigation);
    }

    /// <summary>
    /// Public static method that performs import - can be called from hamburger menu or anywhere
    /// </summary>
    public static async Task PerformImportAsync(ContentPage page, INavigation navigation)
    {
        // Static lock to prevent multiple imports across all pages
        if (_isImportingStatic) 
        {
            System.Diagnostics.Debug.WriteLine($"[ReviewersPage] Import already in progress (static check)");
            return;
        }
        _isImportingStatic = true;
        
        System.Diagnostics.Debug.WriteLine($"[ReviewersPage] PerformImportAsync called from {page.GetType().Name}");
        
        try
        {
            // JSON only
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "application/json" } },
                { DevicePlatform.iOS, new[] { "public.json" } },
                { DevicePlatform.MacCatalyst, new[] { "public.json" } },
                { DevicePlatform.WinUI, new[] { ".json" } },
            });

            var pick = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select JSON export file",
                FileTypes = fileTypes
            });
            
            if (pick is null)
            {
                System.Diagnostics.Debug.WriteLine($"[ReviewersPage] User cancelled file picker");
                return;
            }
            
            var extension = Path.GetExtension(pick.FileName)?.ToLowerInvariant();
            if (extension != ".json")
            {
                await page.ShowPopupAsync(new AppModal("Import", "Only JSON files are supported.", "OK"));
                return;
            }

            string content;
            using (var stream = await pick.OpenReadAsync())
            using (var reader = new StreamReader(stream))
                content = await reader.ReadToEndAsync();

            // Check if encrypted
            if (ExportEncryptionService.IsEncrypted(content))
            {
                bool passwordCorrect = false;
                
                while (!passwordCorrect)
                {
                    // Ask for password using custom modal
                    var passwordModal = new PasswordInputModal(
                        "Password Required",
                        "This file is password-protected. Enter the password:",
                        "Password");
                    
                    var passwordResult = await page.ShowPopupAsync(passwordModal);
                    var password = passwordResult as string;
                    
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        // User cancelled password entry
                        System.Diagnostics.Debug.WriteLine($"[ReviewersPage] User cancelled password entry");
                        return;
                    }
                    
                    try
                    {
                        content = ExportEncryptionService.Decrypt(content, password);
                        passwordCorrect = true;
                        System.Diagnostics.Debug.WriteLine($"[ReviewersPage] Decryption successful");
                    }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        var retry = await page.ShowPopupAsync(new InfoModal(
                            "Incorrect Password",
                            "The password you entered is incorrect. Would you like to try again?",
                            "Try Again",
                            "Cancel"));
                        
                        var shouldRetry = retry is bool b && b;
                        
                        if (!shouldRetry)
                        {
                            // User chose to cancel
                            System.Diagnostics.Debug.WriteLine($"[ReviewersPage] User cancelled after incorrect password");
                            return;
                        }
                        // Loop continues to ask for password again
                    }
                    catch (Exception ex)
                    {
                        await page.ShowPopupAsync(new AppModal("Import Failed", $"Decryption error: {ex.Message}", "OK"));
                        return;
                    }
                }
            }

            // Parse JSON
            var (title, cards, progressData) = ParseJsonExportStatic(content);

            if (cards.Count == 0)
            {
                await page.ShowPopupAsync(new AppModal("Import", "No cards found in file.", "OK"));
                return;
            }

            var importPage = new ImportPage(title, cards);
            if (!string.IsNullOrEmpty(progressData))
            {
                importPage.SetProgressData(progressData);
            }
            
            // If called from hamburger menu (not ReviewersPage), set flag to navigate to ReviewersPage after import
            if (page is not ReviewersPage)
            {
                importPage.SetNavigateToReviewersPage(true);
                System.Diagnostics.Debug.WriteLine($"[ReviewersPage] SetNavigateToReviewersPage(true) for hamburger import");
            }
            
            await Navigator.PushAsync(importPage, navigation);
            System.Diagnostics.Debug.WriteLine($"[ReviewersPage] Import successful");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReviewersPage] Import failed: {ex.Message}");
            await page.ShowPopupAsync(new AppModal("Import Failed", ex.Message, "OK"));
        }
        finally
        {
            _isImportingStatic = false;
            System.Diagnostics.Debug.WriteLine($"[ReviewersPage] Import lock released");
        }
    }

    private static bool _isImportingStatic = false;

    private static (string Title, List<(string Q, string A)> Cards, string ProgressData) ParseJsonExportStatic(string json)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var export = System.Text.Json.JsonSerializer.Deserialize<Models.ReviewerExport>(json, options);
            if (export == null)
            {
                return ("Imported Reviewer", new List<(string, string)>(), string.Empty);
            }

            var cards = export.Cards?
                .Select(c => (c.Question ?? string.Empty, c.Answer ?? string.Empty))
                .ToList() ?? new List<(string, string)>();

            var progressData = export.Progress?.Enabled == true && !string.IsNullOrEmpty(export.Progress?.Data)
                ? export.Progress.Data
                : string.Empty;

            return (export.Title ?? "Imported Reviewer", cards, progressData);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewersPage] JSON parse error: {ex.Message}");
            throw new InvalidDataException("Invalid JSON export file format.");
        }
    }

    /// <summary>
    /// Load SRS progress data and count cards at each mastery level (Learned, Skilled, Memorized)
    /// </summary>
    private (int learned, int skilled, int memorized) LoadSrsMasteryCounts(int reviewerId, List<Flashcard>? cards)
    {
        try
        {
            if (cards == null || cards.Count == 0)
                return (0, 0, 0);

            // Load saved SRS progress from Preferences
            var progressKey = $"ReviewState_{reviewerId}";
            var payload = Preferences.Get(progressKey, null);
            
            if (string.IsNullOrWhiteSpace(payload))
            {
                // No progress saved yet - all cards are at Avail stage
                return (0, 0, 0);
            }

            // Parse the saved progress
            var list = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(payload);
            if (list == null)
                return (0, 0, 0);

            int learnedCount = 0;
            int skilledCount = 0;
            int memorizedCount = 0;

            foreach (var dto in list)
            {
                try
                {
                    var stageStr = dto.GetProperty("Stage").GetString();
                    if (Enum.TryParse<mindvault.Srs.Stage>(stageStr, out var stage))
                    {
                        if (stage >= mindvault.Srs.Stage.Learned)
                            learnedCount++;
                        if (stage >= mindvault.Srs.Stage.Skilled)
                            skilledCount++;
                        if (stage == mindvault.Srs.Stage.Memorized)
                            memorizedCount++;
                    }
                }
                catch
                {
                    // Skip malformed entries
                    continue;
                }
            }

            return (learnedCount, skilledCount, memorizedCount);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    /// <summary>
    /// Calculate how many cards are currently due for review
    /// </summary>
    private int CalculateDueCount(int reviewerId, List<Flashcard>? cards)
    {
        try
        {
            if (cards == null || cards.Count == 0)
                return 0;

            var now = DateTime.UtcNow;

            // Load saved SRS progress from Preferences
            var progressKey = $"ReviewState_{reviewerId}";
            var payload = Preferences.Get(progressKey, null);
            
            if (string.IsNullOrWhiteSpace(payload))
            {
                // No progress saved yet - no cards are due, only new cards available
                return 0;
            }

            // Parse the saved progress
            var list = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(payload);
            if (list == null)
                return 0;

            int dueCount = 0;

            foreach (var dto in list)
            {
                try
                {
                    var stageStr = dto.GetProperty("Stage").GetString();
                    
                    // Skip cards that are still available (not yet introduced)
                    if (stageStr == "Avail")
                        continue;
                    
                    // Get the due date/time
                    var dueAt = dto.GetProperty("DueAt").GetDateTime();
                    
                    // Get cooldown time if it exists
                    DateTime cooldownUntil = now;
                    if (dto.TryGetProperty("CooldownUntil", out var cooldownProp))
                    {
                        cooldownUntil = cooldownProp.GetDateTime();
                    }
                    
                    // Card is due if current time is past both DueAt and CooldownUntil
                    if (now >= dueAt && now >= cooldownUntil)
                    {
                        dueCount++;
                    }
                }
                catch
                {
                    // Skip malformed entries
                    continue;
                }
            }

            return dueCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Hidden Developer Tools - Triple-tap the REVIEWERS title to access
    /// </summary>
    private async void OnTitleTripleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            Debug.WriteLine("[ReviewersPage] Developer Tools accessed via triple-tap");
            await Navigator.PushAsync(new DeveloperToolsPage(), Navigation);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewersPage] Failed to open Developer Tools: {ex.Message}");
        }
    }
}
