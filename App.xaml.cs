using mindvault.Services;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace mindvault;

public partial class App : Application
{
    // Temporary storage for generated flashcards across navigation
    public static List<Models.FlashcardItem> GeneratedFlashcards { get; set; } = new();

    readonly GlobalDeckPreloadService _preloader;

    public App(GlobalDeckPreloadService preloader)
    {
        InitializeComponent();
        _preloader = preloader;
        // Window creation is handled by overriding CreateWindow.
        // Avoid setting deprecated Application.MainPage.
        _ = Task.Run(async () => { try { await _preloader.PreloadAllAsync(); } catch { } });
    }

    // Override CreateWindow instead of setting MainPage to avoid deprecated API usage.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();
        _ = Task.Run(async () => 
        { 
            try 
            { 
                await _preloader.PreloadAllAsync();
                await CleanupEmptyDecksAsync(); // Remove empty/incomplete decks
            } 
            catch { } 
        });
    }
    
    /// <summary>
    /// Cleans up decks with fewer than 5 cards on app startup.
    /// This prevents accumulation of incomplete/abandoned decks.
    /// </summary>
    private async Task CleanupEmptyDecksAsync()
    {
        try
        {
            var db = ServiceHelper.GetRequiredService<Services.DatabaseService>();
            var reviewers = await db.GetReviewersAsync();
            
            foreach (var reviewer in reviewers)
            {
                var cards = await db.GetFlashcardsAsync(reviewer.Id);
                if (cards.Count < 5)
                {
                    await db.DeleteReviewerCascadeAsync(reviewer.Id);
                    System.Diagnostics.Debug.WriteLine($"[App] Cleaned up deck '{reviewer.Title}' with {cards.Count} cards");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Cleanup failed: {ex.Message}");
        }
    }

    protected override async void OnResume()
    {
        base.OnResume();
        try { await _preloader.PreloadAllAsync(); } catch { }
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        try { _preloader.Clear(); } catch { }
    }
}
