using mindvault.Services;
using mindvault.Utils;
using System.Diagnostics;
using mindvault.Data;
using Microsoft.Maui.Storage;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using mindvault.Models;
using CommunityToolkit.Maui.Views;

namespace mindvault.Pages;

public partial class ImportPage : ContentPage
{
    // Preview data
    public string ReviewerTitle { get; private set; }
    public int Questions => Cards?.Count ?? 0;
    public string QuestionsText => Questions.ToString();

    public ObservableCollection<CardPreview> Cards { get; private set; } = new();

    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();

    // Preview ctor
    public ImportPage(string reviewerTitle, List<(string Q, string A)> cards)
    {
        InitializeComponent();
        ReviewerTitle = reviewerTitle;
        foreach (var c in cards)
            Cards.Add(new CardPreview { Question = c.Q, Answer = c.A });
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this, "Burger", "MainMenu");
    }

    // Legacy/demo ctor kept (not used in new flow)
    public ImportPage(string reviewerTitle = "Science Reviewer", int questions = 75)
    {
        InitializeComponent();
        ReviewerTitle = reviewerTitle;
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this, "Burger", "MainMenu");
    }

    private string _progressData = string.Empty;
    private bool _isImporting = false;
    
    private async void OnImportTapped(object? sender, EventArgs e)
    {
        // Prevent multiple simultaneous imports
        if (_isImporting)
        {
            Debug.WriteLine($"[ImportPage] Import already in progress, ignoring click");
            return;
        }
        
        _isImporting = true;
        Debug.WriteLine($"[ImportPage] ========== STARTING IMPORT ==========");
        Debug.WriteLine($"[ImportPage] Progress data available: {!string.IsNullOrEmpty(_progressData)}");
        Debug.WriteLine($"[ImportPage] Card count: {Cards?.Count ?? 0}");
        
        try
        {
            if (Cards is null || Cards.Count == 0)
            {
                Debug.WriteLine($"[ImportPage] ERROR: No cards to import");
                await PageHelpers.SafeDisplayAlertAsync(this, "Import", "No preview data to import.", "OK");
                return;
            }

            // Check if progress data exists and ask user what to do
            bool useProgress = false;
            if (!string.IsNullOrEmpty(_progressData))
            {
                Debug.WriteLine($"[ImportPage] Showing progress detection modal");
                var result = await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                    "Progress Detected",
                    "This file contains saved progress. Would you like to continue from where you left off, or start fresh?",
                    "Continue with Progress",
                    "Start Fresh"));
                
                useProgress = result is bool b && b;
                Debug.WriteLine($"[ImportPage] User choice - Use progress: {useProgress}");
                
                // Shorter delay - modal is already dismissed
                await Task.Delay(100);
            }
            else
            {
                Debug.WriteLine($"[ImportPage] No progress data detected in file");
            }

            Debug.WriteLine($"[ImportPage] Creating reviewer with title: {ReviewerTitle}");
            var finalTitle = await EnsureUniqueTitleAsync(ReviewerTitle);
            Debug.WriteLine($"[ImportPage] Final unique title: {finalTitle}");
            
            var reviewer = new Reviewer { Title = finalTitle, CreatedUtc = DateTime.UtcNow };
            await _db.AddReviewerAsync(reviewer);
            Debug.WriteLine($"[ImportPage] Created reviewer with ID: {reviewer.Id}");

            Debug.WriteLine($"[ImportPage] Adding {Cards.Count} flashcards...");
            int order = 1;
            var addedCards = new List<Flashcard>();
            foreach (var c in Cards)
            {
                var flashcard = new Flashcard
                {
                    ReviewerId = reviewer.Id,
                    Question = c.Question,
                    Answer = c.Answer,
                    Learned = false,
                    Order = order++
                };
                await _db.AddFlashcardAsync(flashcard);
                addedCards.Add(flashcard);
            }
            Debug.WriteLine($"[ImportPage] ✓ Added {addedCards.Count} flashcards");

            // Update global deck preloader cache
            var preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();
            preloader.Decks[reviewer.Id] = addedCards;
            Debug.WriteLine($"[ImportPage] ✓ Updated preloader cache");

            // Import progress data if user chose to continue
            bool progressImported = false;
            if (useProgress)
            {
                Debug.WriteLine($"[ImportPage] Attempting to import progress data...");
                progressImported = ImportProgressData(reviewer.Id);
                Debug.WriteLine($"[ImportPage] Progress import result: {progressImported}");
                
                // Shorter cooldown after progress import
                if (progressImported)
                {
                    await Task.Delay(100);
                    Debug.WriteLine($"[ImportPage] Progress import cooldown complete");
                }
            }
            else
            {
                Debug.WriteLine($"[ImportPage] User chose to start fresh, skipping progress import");
            }
            
            var message = progressImported 
                ? $"Imported '{finalTitle}' with {Cards.Count} cards and progress data."
                : $"Imported '{finalTitle}' with {Cards.Count} cards.";
            
            Debug.WriteLine($"[ImportPage] Showing success message: {message}");
            await PageHelpers.SafeDisplayAlertAsync(this, "Import", message, "OK");
            
            // Shorter delay - SafeDisplayAlertAsync already waits for dismissal
            await Task.Delay(100);
            
            Debug.WriteLine($"[ImportPage] Navigating back...");
            
            // Use MainThread to ensure navigation happens on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Navigation.PopAsync();
                    Debug.WriteLine($"[ImportPage] ✓ Navigation successful");
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"[ImportPage] Navigation error: {navEx.Message}");
                    // Try alternative navigation
                    try
                    {
                        await Shell.Current.GoToAsync("///ReviewersPage");
                        Debug.WriteLine($"[ImportPage] ✓ Alternative navigation successful");
                    }
                    catch (Exception shellEx)
                    {
                        Debug.WriteLine($"[ImportPage] Shell navigation error: {shellEx.Message}");
                    }
                }
            });
            
            Debug.WriteLine($"[ImportPage] ========== IMPORT COMPLETE ==========");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportPage] ========== IMPORT FAILED ==========");
            Debug.WriteLine($"[ImportPage] ERROR: {ex.Message}");
            Debug.WriteLine($"[ImportPage] Stack trace: {ex.StackTrace}");
            await PageHelpers.SafeDisplayAlertAsync(this, "Import Failed", ex.Message, "OK");
        }
        finally
        {
            _isImporting = false;
            Debug.WriteLine($"[ImportPage] Import lock released");
        }
    }

    private async Task<string> EnsureUniqueTitleAsync(string title)
    {
        var existing = await _db.GetReviewersAsync();
        if (!existing.Any(r => string.Equals(r.Title, title, StringComparison.OrdinalIgnoreCase)))
            return title;
        int i = 2;
        while (true)
        {
            var candidate = $"{title} ({i})";
            if (!existing.Any(r => string.Equals(r.Title, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            i++;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mindvault.Utils.AnimHelpers.SlideFadeInAsync(Content);
    }

    public void SetProgressData(string progressData)
    {
        _progressData = progressData;
    }

    private bool ImportProgressData(int reviewerId)
    {
        try
        {
            if (string.IsNullOrEmpty(_progressData))
            {
                Debug.WriteLine($"[ImportPage] No progress data to import");
                return false;
            }
            
            Debug.WriteLine($"[ImportPage] Starting progress import for reviewer {reviewerId}");
            Debug.WriteLine($"[ImportPage] Progress data length: {_progressData.Length} characters");
            
            string progressJson;
            
            // Try to parse directly as JSON first (new format)
            try
            {
                var test = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(_progressData);
                if (test != null && test.Count > 0)
                {
                    // It's already JSON, use directly
                    progressJson = _progressData;
                    Debug.WriteLine($"[ImportPage] Progress data is plain JSON format");
                }
                else
                {
                    Debug.WriteLine($"[ImportPage] Progress data parsed but empty");
                    return false;
                }
            }
            catch
            {
                // Not JSON, try base64 decode (legacy TXT format)
                try
                {
                    var bytes = Convert.FromBase64String(_progressData);
                    progressJson = System.Text.Encoding.UTF8.GetString(bytes);
                    Debug.WriteLine($"[ImportPage] Progress data was base64 encoded (legacy format)");
                }
                catch (Exception decodeEx)
                {
                    Debug.WriteLine($"[ImportPage] Failed to decode base64: {decodeEx.Message}");
                    return false;
                }
            }
            
            Debug.WriteLine($"[ImportPage] Decoded progress JSON length: {progressJson.Length}");
            
            // Parse the old progress data
            var oldProgress = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(progressJson);
            if (oldProgress == null || oldProgress.Count == 0)
            {
                Debug.WriteLine($"[ImportPage] No progress entries found");
                return false;
            }
            
            Debug.WriteLine($"[ImportPage] Found {oldProgress.Count} progress entries");
            
            // Get the newly imported cards (in order)
            var newCards = _db.GetFlashcardsAsync(reviewerId).GetAwaiter().GetResult();
            if (newCards.Count == 0)
            {
                Debug.WriteLine($"[ImportPage] No cards found for reviewer {reviewerId}");
                return false;
            }
            
            Debug.WriteLine($"[ImportPage] Found {newCards.Count} cards for reviewer {reviewerId}");
            
            // Map progress by card position (order) instead of ID
            // This works because cards are imported in the same order as they were exported
            var newProgress = new List<object>();
            for (int i = 0; i < Math.Min(oldProgress.Count, newCards.Count); i++)
            {
                var oldCard = oldProgress[i];
                var newCard = newCards[i];
                
                // Create new progress entry with NEW card ID but OLD progress data
                var progressEntry = new
                {
                    Id = newCard.Id, // Use NEW card ID
                    Stage = oldCard.GetProperty("Stage").GetString(),
                    DueAt = oldCard.GetProperty("DueAt").GetDateTime(),
                    Ease = oldCard.GetProperty("Ease").GetDouble(),
                    IntervalDays = oldCard.GetProperty("IntervalDays").GetDouble(),
                    CorrectOnce = oldCard.GetProperty("CorrectOnce").GetBoolean(),
                    ConsecutiveCorrects = oldCard.GetProperty("ConsecutiveCorrects").GetInt32(),
                    CountedSkilled = oldCard.GetProperty("CountedSkilled").GetBoolean(),
                    CountedMemorized = oldCard.GetProperty("CountedMemorized").GetBoolean(),
                    SeenCount = oldCard.TryGetProperty("SeenCount", out var sc) ? sc.GetInt32() : 0,
                    Repetitions = oldCard.TryGetProperty("Repetitions", out var rep) ? rep.GetInt32() : 0
                };
                
                newProgress.Add(progressEntry);
                Debug.WriteLine($"[ImportPage] Mapped progress: Old ID -> New ID {newCard.Id}, Stage: {progressEntry.Stage}");
            }
            
            // Serialize and save the remapped progress
            var newProgressJson = System.Text.Json.JsonSerializer.Serialize(newProgress);
            var progressKey = $"ReviewState_{reviewerId}";
            Preferences.Set(progressKey, newProgressJson);
            
            Debug.WriteLine($"[ImportPage] ✓ Successfully imported and remapped progress data for {newProgress.Count} cards (reviewer {reviewerId})");
            Debug.WriteLine($"[ImportPage] Progress key: {progressKey}");
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportPage] ERROR: Failed to import progress: {ex.Message}");
            Debug.WriteLine($"[ImportPage] Stack trace: {ex.StackTrace}");
            return false;
        }
    }
}
