using mindvault.Services;
using mindvault.Utils;
using mindvault.Controls;
using System.Diagnostics;
using mindvault.Data;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using System.Threading;

namespace mindvault.Pages;

public partial class TitleReviewerPage : ContentPage
{
    readonly DatabaseService _db;
    readonly SemaphoreSlim _createLock = new(1, 1);

    public TitleReviewerPage()
    {
        InitializeComponent();
        PageHelpers.SetupHamburgerMenu(this, "Burger", "MainMenu");
        _db = ServiceHelper.GetRequiredService<DatabaseService>();
    }

    private async void OnCreateNewTapped(object sender, EventArgs e)
    {
        await CreateNewReviewerAsync();
    }

    private async void OnTitleEntryCompleted(object? sender, EventArgs e)
    {
        // Called when user presses Enter/Done on keyboard
        await CreateNewReviewerAsync();
    }

    /// <summary>
    /// Shows the AppModal popup and waits for it to be dismissed.
    /// Uses TaskCompletionSource with proper event handling to ensure completion.
    /// </summary>
    private async Task ShowModalAlertAsync(string title, string message, string buttonText)
    {
        var tcs = new TaskCompletionSource<bool>();
        var modal = new AppModal(title, message, buttonText);
        
        // Subscribe to Closed event BEFORE showing the popup
        EventHandler<CommunityToolkit.Maui.Core.PopupClosedEventArgs>? closedHandler = null;
        closedHandler = (s, e) =>
        {
            modal.Closed -= closedHandler;
            tcs.TrySetResult(true);
        };
        modal.Closed += closedHandler;
        
        // Show the popup
        this.ShowPopup(modal);
        
        // Wait for completion with timeout as safety net
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(30000));
        if (completedTask != tcs.Task)
        {
            Debug.WriteLine("[TitleReviewerPage] Modal timeout - forcing completion");
            tcs.TrySetResult(true);
        }
    }

    private async Task CreateNewReviewerAsync()
    {
        // Use TryWait to immediately return if already processing (no queuing)
        if (!await _createLock.WaitAsync(0))
        {
            Debug.WriteLine("[TitleReviewerPage] CreateNewReviewerAsync already in progress, ignoring");
            return;
        }
        
        Debug.WriteLine("[TitleReviewerPage] CreateNewReviewerAsync started");
        
        try
        {
            var title = TitleEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                Debug.WriteLine("[TitleReviewerPage] Title is empty, showing modal");
                await ShowModalAlertAsync("Oops", "Please enter a title for your reviewer.", "OK");
                Debug.WriteLine("[TitleReviewerPage] Modal dismissed, returning");
                return;
            }

            // Create reviewer row
            var reviewer = new Reviewer { Title = title };
            await _db.AddReviewerAsync(reviewer);

            Debug.WriteLine($"[TitleReviewerPage] Created reviewer #{reviewer.Id} '{reviewer.Title}' -> AddFlashcardsPage");

            // Navigate to editor, passing id and title (single-shot)
            await PageHelpers.SafeNavigateAsync(this,
                async () => await Shell.Current.GoToAsync($"///AddFlashcardsPage?id={reviewer.Id}&title={Uri.EscapeDataString(reviewer.Title)}"),
                "Could not open add flashcards page");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TitleReviewerPage] Error: {ex.Message}");
            await ShowModalAlertAsync("Error", "Could not create reviewer. Please try again.", "OK");
        }
        finally
        {
            _createLock.Release();
            Debug.WriteLine("[TitleReviewerPage] CreateNewReviewerAsync finished, lock released");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reset the semaphore when page appears in case it was left in a bad state
        // Only release if it's currently held (CurrentCount == 0)
        if (_createLock.CurrentCount == 0)
        {
            try { _createLock.Release(); } catch { }
        }
        _ = RunEntryAnimationAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { if (TitleEntry is not null) TitleEntry.Text = string.Empty; } catch { }
    }

    async Task RunEntryAnimationAsync()
    {
        try
        {
            await Task.Delay(50); // let layout complete
            await mindvault.Utils.AnimHelpers.SlideFadeInAsync(Content);
        }
        catch { }
    }
}