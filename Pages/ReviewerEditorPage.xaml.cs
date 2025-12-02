using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Globalization;
using mindvault.Services;
using mindvault.Utils;
using System.Diagnostics;
using mindvault.Data;
using Microsoft.Maui.Storage;

namespace mindvault.Pages;

[QueryProperty(nameof(ReviewerId), "id")]
[QueryProperty(nameof(ReviewerTitle), "title")]
public partial class ReviewerEditorPage : ContentPage, INotifyPropertyChanged
{
    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();
    readonly GlobalDeckPreloadService _preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();

    const int MinCards = 5; // required minimum contentful cards per deck

    int _reviewerId;
    public int ReviewerId
    {
        get => _reviewerId;
        set 
        { 
            if (_reviewerId == value) return;
            
            // Clear items when switching to a different deck
            if (_reviewerId != value && _reviewerId > 0)
            {
                Items.Clear();
            }
            
            _reviewerId = value; 
            OnPropertyChanged(); 
            
            // Load cards for the new reviewer
            if (value > 0) 
            {
                LoadCardsAsync(); 
            }
        }
    }

    string _reviewerTitle = string.Empty;
    public string ReviewerTitle
    {
        get => _reviewerTitle;
        set { if (_reviewerTitle == value) return; _reviewerTitle = value ?? string.Empty; OnPropertyChanged(); }
    }

    public ObservableCollection<ReviewItem> Items { get; } = new();

    bool _allowNavigationOnce; // set when we already handled warning & are programmatically navigating

    bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
    }

    public ReviewerEditorPage()
    {
        InitializeComponent();
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this);
        SetupKeyboardShortcuts();
    }

    // Setup keyboard shortcuts using Windows-specific handlers
    private void SetupKeyboardShortcuts()
    {
#if WINDOWS
        // Hook into Windows keyboard events when page appears
        this.Loaded += OnPageLoaded;
#endif
    }

#if WINDOWS
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        try
        {
            // Get the native Windows window and hook keyboard to Content (WinUI 3 method)
            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window &&
                window.Content is Microsoft.UI.Xaml.UIElement content)
            {
                content.KeyDown += OnWindowsKeyDown;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Failed to setup keyboard shortcuts: {ex.Message}");
        }
    }

    private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        try
        {
            // Check for modifier keys using InputKeyboardSource (WinUI 3 method)
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            // Only process if this page is currently visible
            if (Shell.Current?.CurrentPage != this)
                return;

            switch (e.Key)
            {
                case Windows.System.VirtualKey.S when ctrl && !shift:
                    MainThread.BeginInvokeOnMainThread(SaveCurrentEditingCardAndCreateNew);
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.N when ctrl && !shift:
                    MainThread.BeginInvokeOnMainThread(AddNewCard);
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.D when ctrl && !shift:
                    MainThread.BeginInvokeOnMainThread(async () => await DeleteCurrentCard());
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.Enter when ctrl:
                    MainThread.BeginInvokeOnMainThread(async () => await AttemptExitAsync());
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.E when ctrl && !shift:
                    MainThread.BeginInvokeOnMainThread(ShowTitleEditor);
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.H when ctrl && !shift:
                    MainThread.BeginInvokeOnMainThread(ShowShortcutsModal);
                    e.Handled = true;
                    break;

                case Windows.System.VirtualKey.Escape:
                    MainThread.BeginInvokeOnMainThread(HandleEscapeKey);
                    e.Handled = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Keyboard event error: {ex.Message}");
        }
    }
#endif

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Only show loading if we haven't loaded cards yet
        if (Items.Count == 0) 
            IsLoading = true;
        
        // Run animation concurrently with data loading (don't await here)
        _ = AnimHelpers.SlideFadeInAsync(Content);
        
        if (Shell.Current is not null)
            Shell.Current.Navigating += OnShellNavigating;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Shell.Current is not null)
            Shell.Current.Navigating -= OnShellNavigating;

#if WINDOWS
        try
        {
            // Unhook keyboard event
            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window &&
                window.Content is Microsoft.UI.Xaml.UIElement content)
            {
                content.KeyDown -= OnWindowsKeyDown;
            }
        }
        catch { }
#endif
    }

    async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        try
        {
            if (_allowNavigationOnce) { _allowNavigationOnce = false; return; }
            // Only guard if current page is this editor and target is different page
            if (Shell.Current?.CurrentPage != this) return;
            // Ignore internal self-navigation
            if (e.Target.Location.OriginalString.Contains(nameof(ReviewerEditorPage))) return;

            int contentful = Items.Count(i => HasContent(i));
            if (contentful >= MinCards)
            {
                // Save deck silently then allow navigation
                await SaveAllAsync();
                return;
            }

            // Cancel navigation and prompt
            e.Cancel();
            var leaveResult = await this.ShowPopupAsync(new AppModal("Incomplete Deck", $"This deck has only {contentful} card(s). Leaving will DELETE the deck. Continue?", "Delete & Exit", "Stay"));
            bool leave = leaveResult is bool b && b;
            if (!leave) return;

            // Delete and then navigate manually to the original target
            try
            {
                await EnsureReviewerIdAsync();
                if (ReviewerId > 0)
                    await _db.DeleteReviewerCascadeAsync(ReviewerId);
            }
            catch { }
            _allowNavigationOnce = true; // allow next navigation
            await Shell.Current.GoToAsync(e.Target.Location, true);
        }
        catch { }
    }

    async Task EnsureReviewerIdAsync()
    {
        if (ReviewerId > 0) return;
        if (string.IsNullOrWhiteSpace(ReviewerTitle)) return;
        
        // Fast path: check if it's already in the preloader cache
        if (_preloader.Decks.ContainsKey(ReviewerId))
            return;
        
        try
        {
            // Only query database if we don't have the ID
            var reviewers = await _db.GetReviewersAsync();
            var match = reviewers.FirstOrDefault(r => r.Title == ReviewerTitle);
            if (match is not null) ReviewerId = match.Id;
        }
        catch { }
    }

    async void LoadCardsAsync()
    {
        try
        {
            // Fast path: if we already have ReviewerId, skip the lookup
            if (ReviewerId <= 0)
                await EnsureReviewerIdAsync();
            
            if (ReviewerId <= 0) return;

            IsLoading = true;

            List<Flashcard> cards;
            
            // Always try RAM cache first
            if (_preloader.Decks.TryGetValue(ReviewerId, out var fromRam))
            {
                cards = fromRam;
            }
            else
            {
                // Fallback: load from DB if not yet preloaded
                cards = await _db.GetFlashcardsAsync(ReviewerId);
                // Update preloader store for future reads
                _preloader.Decks[ReviewerId] = cards.ToList();
            }

            // Always clear before repopulating to ensure fresh data
            Items.Clear();
            
            foreach (var c in cards)
            {
                Items.Add(new ReviewItem
                {
                    Id = c.Id,
                    Question = c.Question,
                    Answer = c.Answer,
                    QuestionImagePath = c.QuestionImagePath,
                    AnswerImagePath = c.AnswerImagePath,
                    IsSaved = true,
                    Number = c.Order
                });
            }
            
            RenumberSaved();
            
            // For new decks with no cards, add one empty card to start
            if (Items.Count == 0) 
                Items.Add(new ReviewItem());

            IsLoading = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] LoadCardsAsync error: {ex.Message}");
            IsLoading = false;
        }
    }

    // === Shortcuts Modal ===
    void OnShortcutsTapped(object? sender, TappedEventArgs e)
    {
        ShowShortcutsModal();
    }

    private void ShowShortcutsModal()
    {
        try
        {
            var modal = new ShortcutsModal();
            this.ShowPopup(modal);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Failed to show shortcuts: {ex.Message}");
        }
    }

    // === Keyboard Shortcut Helpers ===
    private void SaveCurrentEditingCardAndCreateNew()
    {
        try
        {
            var editing = Items.FirstOrDefault(x => !x.IsSaved);
            if (editing is not null && HasContent(editing))
            {
                editing.IsSaved = true;
                RenumberSaved();
                _ = SaveAllAsync();
                // Create new card
                Items.Add(new ReviewItem());
                Debug.WriteLine("[ReviewerEditorPage] Saved card and created new via Ctrl+S");
            }
            else if (editing is not null && !HasContent(editing))
            {
                // Current card is empty, just focus on it
                Debug.WriteLine("[ReviewerEditorPage] Current card is empty, no action taken");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Save shortcut error: {ex.Message}");
        }
    }

    private async Task DeleteCurrentCard()
    {
        try
        {
            // Try to find the currently editing card first
            var editing = Items.FirstOrDefault(x => !x.IsSaved);
            if (editing is not null)
            {
                // Confirm deletion using AppModal
                var confirmResult = await this.ShowPopupAsync(new AppModal(
                    "Delete Card", 
                    "Are you sure you want to delete this card?", 
                    "Delete", 
                    "Cancel"));
                
                bool confirmed = confirmResult is bool b && b;
                if (!confirmed) return;

                int currentSaved = Items.Count(i => i.IsSaved && HasContent(i));
                int delta = (editing.IsSaved && HasContent(editing)) ? 1 : 0;
                if (currentSaved - delta < MinCards)
                {
                    await this.ShowPopupAsync(new AppModal(
                        "Minimum Cards", 
                        $"Deleting this would leave fewer than {MinCards} cards (deck will be deleted if you exit without adding more).", 
                        "OK"));
                }

                await EnsureReviewerIdAsync();
                Items.Remove(editing);
                RenumberSaved();
                await SaveAllAsync();
                Debug.WriteLine("[ReviewerEditorPage] Deleted card via Ctrl+D");
            }
            else
            {
                await this.ShowPopupAsync(new AppModal(
                    "Delete Card", 
                    "No card is currently being edited.", 
                    "OK"));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Delete shortcut error: {ex.Message}");
        }
    }

    private void AddNewCard()
    {
        try
        {
            var editing = Items.LastOrDefault(x => !x.IsSaved);
            if (editing is not null)
            {
                editing.IsSaved = true;
                RenumberSaved();
                _ = SaveAllAsync();
            }
            Items.Add(new ReviewItem());
            Debug.WriteLine("[ReviewerEditorPage] Added new card via Ctrl+N");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Add new card shortcut error: {ex.Message}");
        }
    }

    private void ShowTitleEditor()
    {
        try
        {
            if (!RenameOverlay.IsVisible)
            {
                RenameEntry.Text = ReviewerTitle;
                RenameOverlay.IsVisible = true;
                Debug.WriteLine("[ReviewerEditorPage] Opened title editor via Ctrl+E");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Show title editor error: {ex.Message}");
        }
    }

    private void HandleEscapeKey()
    {
        try
        {
            if (RenameOverlay.IsVisible)
            {
                RenameOverlay.IsVisible = false;
                Debug.WriteLine("[ReviewerEditorPage] Closed rename overlay via Esc");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReviewerEditorPage] Escape key handler error: {ex.Message}");
        }
    }

    // === Title rename ===
    void OnEditTitleTapped(object? sender, TappedEventArgs e)
    { RenameEntry.Text = ReviewerTitle; RenameOverlay.IsVisible = true; }
    void OnRenameCancel(object? sender, EventArgs e) => RenameOverlay.IsVisible = false;
    async void OnRenameSave(object? sender, EventArgs e)
    {
        var newTitle = RenameEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newTitle)) 
        { 
            this.ShowPopup(new AppModal("Invalid", "Please enter a valid title.", "OK")); 
            return; 
        }
        try 
        { 
            if (ReviewerId > 0) await _db.UpdateReviewerTitleAsync(ReviewerId, newTitle); 
            ReviewerTitle = newTitle; 
        }
        catch (Exception ex) 
        { 
            await this.ShowPopupAsync(new AppModal("Rename Failed", ex.Message, "OK")); 
            return; 
        }
        finally { RenameOverlay.IsVisible = false; }
    }

    // Pick image for the currently editing item
    async void OnPickImageTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is not Element el) return;
            var ctx = el.BindingContext as ReviewItem ?? (el.Parent as Element)?.BindingContext as ReviewItem;
            if (ctx is null) return;
            var side = (e as TappedEventArgs)?.Parameter as string;

            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "image/*" } },
                { DevicePlatform.iOS, new[] { "public.image" } },
                { DevicePlatform.MacCatalyst, new[] { "public.image" } },
                { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg" } },
            });
            var pick = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select image", FileTypes = fileTypes });
            if (pick is null) return;

            var ext = Path.GetExtension(pick.FileName);
            var dest = Path.Combine(FileSystem.AppDataDirectory, $"card_{Guid.NewGuid():N}{ext}");
            using (var src = await pick.OpenReadAsync())
            using (var dst = File.Create(dest))
                await src.CopyToAsync(dst);

            if (string.Equals(side, "A", StringComparison.OrdinalIgnoreCase))
                ctx.AnswerImagePath = dest;
            else
                ctx.QuestionImagePath = dest;

            ctx.OnPropertyChanged(nameof(ReviewItem.QuestionImagePath));
            ctx.OnPropertyChanged(nameof(ReviewItem.AnswerImagePath));
            ctx.OnPropertyChanged(nameof(ReviewItem.QuestionImageVisible));
            ctx.OnPropertyChanged(nameof(ReviewItem.AnswerImageVisible));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Image", ex.Message, "OK");
        }
    }

    // === UI events from XAML ===
    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    { if (sender is not Element el || el.BindingContext is not ReviewItem item) return; item.IsSaved = true; RenumberSaved(); await SaveAllAsync(); }
    private async void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element el || el.BindingContext is not ReviewItem item) return;
        
        var confirmResult = await this.ShowPopupAsync(new AppModal(
            "Delete Card", 
            "Are you sure you want to delete this card?", 
            "Delete", 
            "Cancel"));
        
        bool confirm = confirmResult is bool b && b;
        if (!confirm) return;

        int currentSaved = Items.Count(i => i.IsSaved && HasContent(i));
        int delta = (item.IsSaved && HasContent(item)) ? 1 : 0;
        if (currentSaved - delta < MinCards)
        {
            await this.ShowPopupAsync(new AppModal(
                "Minimum Cards", 
                $"Deleting this would leave fewer than {MinCards} cards (deck will be deleted if you exit without adding more).", 
                "OK"));
        }

        await EnsureReviewerIdAsync();
        Items.Remove(item);
        RenumberSaved();
        await SaveAllAsync();
        await PageHelpers.SafeDisplayAlertAsync(this, "Deleted", "Card removed.");
    }
    private async void OnAddNewTapped(object? sender, TappedEventArgs e)
    { var editing = Items.LastOrDefault(x => !x.IsSaved); if (editing is not null) { editing.IsSaved = true; RenumberSaved(); await SaveAllAsync(); } Items.Add(new ReviewItem()); }
    private async void OnCardTapped(object? sender, TappedEventArgs e)
    { if (sender is not Element el || el.BindingContext is not ReviewItem item) return; var editing = Items.FirstOrDefault(x => !x.IsSaved); if (editing is not null) { editing.IsSaved = true; RenumberSaved(); await SaveAllAsync(); } item.IsSaved = false; }

    // New unified exit handler for check button & hardware back
    private async Task<bool> AttemptExitAsync()
    {
        int contentful = Items.Count(i => HasContent(i));
        if (contentful < MinCards)
        {
            var leaveResult = await this.ShowPopupAsync(new AppModal("Incomplete Deck", $"This deck has only {contentful} card(s). Leaving will DELETE the deck. Continue?", "Delete & Exit", "Stay"));
            bool leave = leaveResult is bool b && b;
            if (!leave) return false;
            try
            {
                await EnsureReviewerIdAsync();
                if (ReviewerId > 0)
                    await _db.DeleteReviewerCascadeAsync(ReviewerId);
            }
            catch { }
            _allowNavigationOnce = true;
            await NavigationService.CloseEditorToReviewers();
            return true;
        }

        bool changed = false;
        foreach (var it in Items.Where(x => !x.IsSaved).ToList())
        {
            if (HasContent(it)) { it.IsSaved = true; changed = true; }
        }
        if (changed) RenumberSaved();
        await SaveAllAsync();
        _allowNavigationOnce = true;
        await NavigationService.CloseEditorToReviewers();
        return true;
    }

    private async void OnCheckTapped(object? sender, EventArgs e)
    {
        await AttemptExitAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = AttemptExitWithBackNavigationAsync();
        return true; // we handle navigation
    }

    // Separate method that tries to go back to previous page instead of always going to ReviewersPage
    private async Task<bool> AttemptExitWithBackNavigationAsync()
    {
        int contentful = Items.Count(i => HasContent(i));
        if (contentful < MinCards)
        {
            var leaveResult = await this.ShowPopupAsync(new AppModal("Incomplete Deck", $"This deck has only {contentful} card(s). Leaving will DELETE the deck. Continue?", "Delete & Exit", "Stay"));
            bool leave = leaveResult is bool b && b;
            if (!leave) return false;
            try
            {
                await EnsureReviewerIdAsync();
                if (ReviewerId > 0)
                    await _db.DeleteReviewerCascadeAsync(ReviewerId);
            }
            catch { }
            _allowNavigationOnce = true;
            
            // Try to go back to previous page, fall back to ReviewersPage
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
            else
            {
                await NavigationService.CloseEditorToReviewers();
            }
            return true;
        }

        bool changed = false;
        foreach (var it in Items.Where(x => !x.IsSaved).ToList())
        {
            if (HasContent(it)) { it.IsSaved = true; changed = true; }
        }
        if (changed) RenumberSaved();
        await SaveAllAsync();
        _allowNavigationOnce = true;
        
        // Try to go back to previous page, fall back to ReviewersPage
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
        }
        else
        {
            await NavigationService.CloseEditorToReviewers();
        }
        return true;
    }

    async Task SaveAllAsync()
    {
        await EnsureReviewerIdAsync();
        if (ReviewerId <= 0) return;

        var saved = Items.Where(x => x.IsSaved && HasContent(x)).ToList();
        if (saved.Count == 0) return;

        // Load existing cards to preserve IDs and avoid resetting progress keys
        var existing = await _db.GetFlashcardsAsync(ReviewerId);
        var existingById = existing.ToDictionary(c => c.Id);

        int order = 1;
        var cardsToCache = new List<Flashcard>();

        foreach (var it in saved)
        {
            if (it.Id > 0 && existingById.TryGetValue(it.Id, out var ex))
            {
                // Update existing card (preserve Id)
                ex.Question = it.Question?.Trim() ?? string.Empty;
                ex.Answer = it.Answer?.Trim() ?? string.Empty;
                ex.QuestionImagePath = it.QuestionImagePath ?? string.Empty;
                ex.AnswerImagePath = it.AnswerImagePath ?? string.Empty;
                ex.Order = order++;
                await _db.UpdateFlashcardAsync(ex);
                cardsToCache.Add(ex);
            }
            else
            {
                // Insert new card
                var card = new Flashcard
                {
                    ReviewerId = ReviewerId,
                    Question = it.Question?.Trim() ?? string.Empty,
                    Answer = it.Answer?.Trim() ?? string.Empty,
                    QuestionImagePath = it.QuestionImagePath ?? string.Empty,
                    AnswerImagePath = it.AnswerImagePath ?? string.Empty,
                    Learned = false,
                    Order = order++
                };
                await _db.AddFlashcardAsync(card);
                // Set newly assigned Id back to item to keep linkage
                it.Id = card.Id;
                cardsToCache.Add(card);
            }
        }

        // Optionally remove cards that were deleted via UI (not present in saved)
        var savedIds = new HashSet<int>(saved.Where(s => s.Id > 0).Select(s => s.Id));
        foreach (var ex in existing)
        {
            if (!savedIds.Contains(ex.Id))
            {
                await _db.DeleteFlashcardAsync(ex.Id);
            }
        }

        // Update memory cache after save
        _preloader.Decks[ReviewerId] = cardsToCache.OrderBy(c => c.Order).ToList();
    }

    static bool HasContent(ReviewItem it)
    {
        return !string.IsNullOrWhiteSpace(it.Question)
            || !string.IsNullOrWhiteSpace(it.Answer)
            || it.QuestionImageVisible
            || it.AnswerImageVisible;
    }

    private void RenumberSaved()
    { int i = 1; foreach (var it in Items.Where(x => x.IsSaved)) it.Number = i++; }

    public class ReviewItem : INotifyPropertyChanged
    {
        int _id;
        string _question = string.Empty;
        string _answer = string.Empty;
        string _qImg = string.Empty;
        string _aImg = string.Empty;
        bool _isSaved;
        int _number;

        public int Id { get => _id; set { if (_id == value) return; _id = value; OnPropertyChanged(); } }
        public string Question { get => _question; set { if (_question == value) return; _question = value ?? string.Empty; OnPropertyChanged(); } }
        public string Answer { get => _answer; set { if (_answer == value) return; _answer = value ?? string.Empty; OnPropertyChanged(); } }
        public string QuestionImagePath { get => _qImg; set { if (_qImg == value) return; _qImg = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(QuestionImageVisible)); } }
        public string AnswerImagePath { get => _aImg; set { if (_aImg == value) return; _aImg = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(AnswerImageVisible)); } }
        public bool QuestionImageVisible => !string.IsNullOrWhiteSpace(QuestionImagePath);
        public bool AnswerImageVisible => !string.IsNullOrWhiteSpace(AnswerImagePath);

        public bool IsSaved { get => _isSaved; set { if (_isSaved == value) return; _isSaved = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditing)); } }
        public bool IsEditing => !_isSaved;

        public int Number { get => _number; set { if (_number == value) return; _number = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}