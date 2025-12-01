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
        if (_isImporting) return;
        _isImporting = true;
        
        Debug.WriteLine($"[ImportPage] OnImportTapped - Starting import process");
        Debug.WriteLine($"[ImportPage] Progress data available: {!string.IsNullOrEmpty(_progressData)}");
        Debug.WriteLine($"[ImportPage] Progress data length: {_progressData?.Length ?? 0} characters");
        
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
                    "This file contains saved progress. Would you like to continue from where you left off?",
                    "Continue",
                    "Start Fresh"));
                useProgress = result is bool b && b;
                Debug.WriteLine($"[ImportPage] User choice - Use progress: {useProgress}");
                Debug.WriteLine($"[ImportPage] Modal result type: {result?.GetType().Name ?? "null"}");
                Debug.WriteLine($"[ImportPage] Modal result value: {result}");
            }
            else
            {
                Debug.WriteLine($"[ImportPage] No progress data detected in file");
            }

            var finalTitle = await EnsureUniqueTitleAsync(ReviewerTitle);
            var reviewer = new Reviewer { Title = finalTitle, CreatedUtc = DateTime.UtcNow };
            await _db.AddReviewerAsync(reviewer);

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

            // Update global deck preloader cache so the count shows immediately
            var preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();
            preloader.Decks[reviewer.Id] = addedCards;

            // Import progress data if user chose to continue
            bool progressImported = false;
            if (useProgress)
            {
                progressImported = ImportProgressData(reviewer.Id);
            }
            
            var message = progressImported 
                ? $"Imported '{finalTitle}' with {Cards.Count} cards and progress data."
                : $"Imported '{finalTitle}' with {Cards.Count} cards.";
            await PageHelpers.SafeDisplayAlertAsync(this, "Import", message, "OK");
            
            // Navigate back and trigger refresh
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await PageHelpers.SafeDisplayAlertAsync(this, "Import Failed", ex.Message, "OK");
        }
        finally
        {
            _isImporting = false;
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
            if (string.IsNullOrEmpty(_progressData)) return false;
            
            // Decode base64 progress data
            var bytes = Convert.FromBase64String(_progressData);
            var progressJson = System.Text.Encoding.UTF8.GetString(bytes);
            
            Debug.WriteLine($"[ImportPage] Original progress JSON length: {progressJson.Length}");
            
            // Parse the old progress data
            var oldProgress = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(progressJson);
            if (oldProgress == null || oldProgress.Count == 0)
            {
                Debug.WriteLine($"[ImportPage] No progress data to import");
                return false;
            }
            
            // Get the newly imported cards (in order)
            var newCards = _db.GetFlashcardsAsync(reviewerId).GetAwaiter().GetResult();
            if (newCards.Count == 0)
            {
                Debug.WriteLine($"[ImportPage] No cards found for reviewer {reviewerId}");
                return false;
            }
            
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
            }
            
            // Serialize and save the remapped progress
            var newProgressJson = System.Text.Json.JsonSerializer.Serialize(newProgress);
            var progressKey = $"ReviewState_{reviewerId}";
            Preferences.Set(progressKey, newProgressJson);
            
            Debug.WriteLine($"[ImportPage] Imported and remapped progress data for {newProgress.Count} cards (reviewer {reviewerId})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportPage] Failed to import progress: {ex.Message}");
            Debug.WriteLine($"[ImportPage] Stack trace: {ex.StackTrace}");
            return false;
        }
    }
}
