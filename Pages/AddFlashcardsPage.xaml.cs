using mindvault.Services;
using mindvault.Utils;
using mindvault.Data;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls; // ensure ContentPage & TappedEventArgs
using CommunityToolkit.Maui.Views; // for ShowPopup/Popups
using System.Collections.Generic;
using System.Linq;
using System.IO; // Path & StreamReader
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Maui.Storage; // for Preferences
using System.Net.Http; // internet check

namespace mindvault.Pages;

[QueryProperty(nameof(ReviewerId), "id")]
[QueryProperty(nameof(ReviewerTitle), "title")]
public partial class AddFlashcardsPage : ContentPage
{
    int _reviewerId;
    public int ReviewerId
    {
        get => _reviewerId;
        set { _reviewerId = value; TryUpdateDeckTitle(); }
    }
    string _reviewerTitle = string.Empty;
    public string ReviewerTitle
    {
        get => _reviewerTitle;
        set { _reviewerTitle = value ?? string.Empty; TryUpdateDeckTitle(); }
    }
    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();
    bool _navigatingForward;
    bool _aiEnvReady = false; // track environment readiness
    bool _cardsAdded = false; // track if user actually added cards

    public AddFlashcardsPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    { base.OnNavigatedTo(args); TryUpdateDeckTitle(); }

    protected override async void OnAppearing()
    { 
        base.OnAppearing(); 
        _navigatingForward = false;
        TryUpdateDeckTitle();
        
        // Editor starts disabled to prevent auto-focus and scrolling
        // It will be enabled when user taps it or clicks "Show Example"
        
        // Run slide animation
        await AnimHelpers.SlideFadeInAsync(Content);
        
        // Load environment flag
        _aiEnvReady = Preferences.Get("ai_env_ready", false);
    }

    void TryUpdateDeckTitle()
    {
        try
        {
            if (DeckTitleLabel != null)
            {
                var title = string.IsNullOrWhiteSpace(ReviewerTitle) ? "" : ReviewerTitle;
                DeckTitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "Deck" : $"Deck: {title}";
            }
        }
        catch { }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        
        // If user is going back without adding cards, delete the empty deck
        if (!_navigatingForward && ReviewerId > 0 && !_cardsAdded)
        {
            try
            {
                var cards = await _db.GetFlashcardsAsync(ReviewerId);
                if (cards.Count == 0)
                {
                    // Delete the empty reviewer
                    await _db.DeleteReviewerCascadeAsync(ReviewerId);
                    System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Deleted empty deck #{ReviewerId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Failed to delete empty deck: {ex.Message}");
            }
        }
        
        // Reset paste editor so examples refresh next time and avoid stale content
        try { if (PasteEditor != null) PasteEditor.Text = string.Empty; } catch { }
        // Stop spinner if still running
        try { if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; } } catch { }
    }

    async Task EnsureReviewerExistsAsync()
    {
        if (ReviewerId > 0) return;
        var title = (ReviewerTitle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title)) title = "Untitled Reviewer";
        var list = await _db.GetReviewersAsync();
        var match = list.FirstOrDefault(r => string.Equals(r.Title, title, StringComparison.OrdinalIgnoreCase));
        if (match is not null) { ReviewerId = match.Id; ReviewerTitle = match.Title; DeckTitleLabel.Text = $"Deck: {ReviewerTitle}"; return; }
        var reviewer = new Reviewer { Title = title, CreatedUtc = DateTime.UtcNow };
        await _db.AddReviewerAsync(reviewer);
        ReviewerId = reviewer.Id; ReviewerTitle = reviewer.Title; DeckTitleLabel.Text = $"Deck: {ReviewerTitle}";
    }

    async void OnClose(object? s, TappedEventArgs e)
    { try { await Shell.Current.GoToAsync("///TitleReviewerPage"); } catch { await Navigation.PopAsync(); } }

    void OnEditorTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            // Enable the Editor when user taps on it
            if (PasteEditor != null && !PasteEditor.IsEnabled)
            {
                PasteEditor.IsEnabled = true;
                
                // Focus the Editor after a small delay to ensure it's ready
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(50);
                    PasteEditor?.Focus();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Failed to enable editor: {ex.Message}");
        }
    }

    void OnShowExampleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            // Enable the Editor first if it's disabled
            if (PasteEditor != null && !PasteEditor.IsEnabled)
            {
                PasteEditor.IsEnabled = true;
            }
            
            if (PasteEditor != null)
            {
                // Insert example format at cursor position or replace all text if empty
                var exampleText = "|(An array of components designed to accomplish a particular objective according to plan.:System)|\n" +
                                  "|(List the OSI Layers:/n1. Physical/n2. Data Link/n3. Network/n4. Transport/n5. Session/n6. Presentation/n7. Application)|\n" +
                                  "|(HTTP Methods:/nGET/nPOST/nPUT/nDELETE:n/a)|\n";
                
                if (string.IsNullOrWhiteSpace(PasteEditor.Text))
                {
                    PasteEditor.Text = exampleText;
                }
                else
                {
                    // Insert at current cursor position
                    var cursorPos = PasteEditor.CursorPosition;
                    var currentText = PasteEditor.Text ?? string.Empty;
                    PasteEditor.Text = currentText.Insert(cursorPos, exampleText);
                    PasteEditor.CursorPosition = cursorPos + exampleText.Length;
                }
                
                // Focus the Editor after inserting text
                PasteEditor.Focus();
                
                // Clear any previous result messages
                if (PasteResultLabel != null)
                {
                    PasteResultLabel.Text = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Failed to show example: {ex.Message}");
        }
    }

    async void OnTypeFlashcards(object? s, TappedEventArgs e)
    { 
        _navigatingForward = true; 
        _cardsAdded = true; // User is going to editor to add cards manually
        await Shell.Current.GoToAsync($"///ReviewerEditorPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}"); 
    }

    async void OnImportPaste(object? s, TappedEventArgs e)
    {
        try
        {
            if (ProcessingIndicator != null) { ProcessingIndicator.IsVisible = true; ProcessingIndicator.IsRunning = true; }
            
            // JSON only (changed from TXT)
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
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return;
            }
            
            // Extension check for JSON only
            var extension = Path.GetExtension(pick.FileName)?.ToLowerInvariant();
            if (extension != ".json") 
            { 
                await DisplayAlert("Import", "Only JSON files are supported.", "OK"); 
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return; 
            }
            
            string content;
            using (var stream = await pick.OpenReadAsync()) 
            using (var reader = new StreamReader(stream)) 
                content = await reader.ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(content))
            {
                await DisplayAlert("Import", "File is empty or could not be read.", "OK");
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return;
            }
            
            // Check if encrypted
            if (mindvault.Services.ExportEncryptionService.IsEncrypted(content))
            {
                bool passwordCorrect = false;
                var passwordAttemptService = ServiceHelper.GetRequiredService<PasswordAttemptService>();
                // Use content hash as identifier - prevents bypass via file renaming
                var fileIdentifier = PasswordAttemptService.GenerateFileIdentifier(content);
                
                while (!passwordCorrect)
                {
                    // Check for lockout first
                    var (isLockedOut, remainingSeconds) = passwordAttemptService.CheckLockout(fileIdentifier);
                    if (isLockedOut)
                    {
                        var lockoutMsg = PasswordAttemptService.FormatLockoutMessage(remainingSeconds);
                        await this.ShowPopupAsync(new mindvault.Controls.AppModal(
                            "Too Many Attempts",
                            $"You have entered incorrect passwords too many times.\n\nPlease wait {lockoutMsg} before trying again.",
                            "OK"));
                        
                        if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                        return;
                    }
                    
                    // Show remaining attempts hint
                    var remainingAttempts = passwordAttemptService.GetRemainingAttempts(fileIdentifier);
                    var attemptHint = remainingAttempts < 3 ? $"\n\n?? {remainingAttempts} attempt{(remainingAttempts > 1 ? "s" : "")} remaining before cooldown." : "";
                    
                    // Ask for password using custom modal
                    var passwordModal = new mindvault.Controls.PasswordInputModal(
                        "Password Required",
                        $"This file is password-protected. Enter the password:{attemptHint}",
                        "Password");
                    
                    var passwordResult = await this.ShowPopupAsync(passwordModal);
                    var password = passwordResult as string;
                    
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        // User cancelled password entry
                        if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                        return;
                    }
                    
                    try
                    {
                        content = mindvault.Services.ExportEncryptionService.Decrypt(content, password);
                        passwordCorrect = true;
                        passwordAttemptService.RecordSuccessfulAttempt(fileIdentifier);
                    }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        // Record the failed attempt
                        var (isNowLockedOut, lockoutSeconds, totalAttempts) = passwordAttemptService.RecordFailedAttempt(fileIdentifier);
                        
                        if (isNowLockedOut)
                        {
                            var lockoutMsg = PasswordAttemptService.FormatLockoutMessage(lockoutSeconds);
                            await this.ShowPopupAsync(new mindvault.Controls.AppModal(
                                "Too Many Attempts",
                                $"You have entered {totalAttempts} incorrect passwords.\n\nPlease wait {lockoutMsg} before trying again.",
                                "OK"));
                            
                            if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                            return;
                        }
                        
                        remainingAttempts = passwordAttemptService.GetRemainingAttempts(fileIdentifier);
                        var retryHint = remainingAttempts > 0 ? $"\n\n{remainingAttempts} attempt{(remainingAttempts > 1 ? "s" : "")} remaining." : "";
                        
                        var retry = await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                            "Incorrect Password",
                            $"The password you entered is incorrect. Would you like to try again?{retryHint}",
                            "Try Again",
                            "Cancel"));
                        
                        var shouldRetry = retry is bool b && b;
                        
                        if (!shouldRetry)
                        {
                            // User chose to cancel
                            if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                            return;
                        }
                        // Loop continues to ask for password again
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Import Failed", $"Decryption error: {ex.Message}", "OK");
                        if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                        return;
                    }
                }
            }
            
            // Parse JSON format
            var (importedTitle, parsed, progressData) = ParseJsonExport(content);
            
            if (parsed.Count == 0)
            {
                await DisplayAlert("Import", "No cards found in file.", "OK");
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return;
            }
            
            // Check if progress data exists and ask user what to do
            bool useProgress = false;
            if (!string.IsNullOrEmpty(progressData))
            {
                var result = await this.ShowPopupAsync(new mindvault.Controls.AppModal(
                    "Progress Detected", 
                    "This file contains saved progress. Would you like to continue from where you left off?",
                    "Continue", "Start Fresh"));
                useProgress = result is bool b && b;
            }
            
            // Ensure reviewer exists
            await EnsureReviewerExistsAsync();
            if (ReviewerId <= 0) 
            { 
                await DisplayAlert("Import", "Failed to create or find reviewer.", "OK");
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return; 
            }
            
            // Cap to maximum of 200 cards
            int total = parsed.Count;
            var capped = parsed.Take(200).ToList();
            
            // Replace any existing flashcards
            await _db.DeleteFlashcardsForReviewerAsync(ReviewerId);
            
            // Invalidate database cache
            _db.InvalidateFlashcardsCache(ReviewerId);
            
            // Add imported cards one by one with proper async handling
            int order = 1;
            var addedCards = new List<Flashcard>();
            foreach (var c in capped)
            {
                var flashcard = new Flashcard
                { 
                    ReviewerId = ReviewerId, 
                    Question = c.Q, 
                    Answer = c.A, 
                    Learned = false, 
                    Order = order++ 
                };
                await _db.AddFlashcardAsync(flashcard);
                addedCards.Add(flashcard);
            }
            
            // Update global deck preloader cache
            var preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();
            preloader.Decks[ReviewerId] = addedCards;
            
            // Import progress data if user chose to continue
            if (useProgress && !string.IsNullOrEmpty(progressData))
            {
                try
                {
                    string progressJson;
                    
                    // Try to parse directly as JSON first (new format)
                    try
                    {
                        var test = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(progressData);
                        if (test != null && test.Count > 0)
                        {
                            // It's already JSON, use directly
                            progressJson = progressData;
                            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Progress data is plain JSON format");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Progress data parsed but empty");
                            throw new Exception("Empty progress data");
                        }
                    }
                    catch
                    {
                        // Not JSON, try base64 decode (legacy TXT format fallback)
                        var bytes = Convert.FromBase64String(progressData);
                        progressJson = System.Text.Encoding.UTF8.GetString(bytes);
                        System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Progress data was base64 encoded (legacy format)");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Decoded progress JSON length: {progressJson.Length}");
                    
                    // Parse the old progress data
                    var oldProgress = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(progressJson);
                    if (oldProgress != null && oldProgress.Count > 0)
                    {
                        // Get the newly imported cards (in order)
                        var newCards = await _db.GetFlashcardsAsync(ReviewerId);
                        
                        // Map progress by card position (order) instead of ID
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
                        var progressKey = $"ReviewState_{ReviewerId}";
                        Preferences.Set(progressKey, newProgressJson);
                        
                        System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Imported and remapped progress data for {newProgress.Count} cards (reviewer {ReviewerId})");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Failed to import progress: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Stack trace: {ex.StackTrace}");
                }
            }
            
            // Verify cards were added successfully
            var verifyCards = await _db.GetFlashcardsAsync(ReviewerId);
            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Import: Added {verifyCards.Count} cards to reviewer {ReviewerId}");
            
            if (verifyCards.Count == 0)
            {
                await DisplayAlert("Import", "Failed to add cards to database.", "OK");
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return;
            }
            
            _cardsAdded = true;
            _navigatingForward = true;
            
            if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
            
            // Add small delay to ensure database commits on Android
            await Task.Delay(150);
            
            // Navigate to editor to allow user to review/edit imported cards
            await Shell.Current.GoToAsync($"///ReviewerEditorPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
        }
        catch (Exception ex)
        { 
            await DisplayAlert("Import Failed", ex.Message, "OK");
            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Import error: {ex}");
        }
        finally 
        { 
            if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; } 
        }
    }

    /// <summary>
    /// Parse JSON export format
    /// </summary>
    static (string Title, List<(string Q, string A)> Cards, string ProgressData) ParseJsonExport(string json)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var export = System.Text.Json.JsonSerializer.Deserialize<mindvault.Models.ReviewerExport>(json, options);
            if (export == null)
            {
                return ("Imported Reviewer", new List<(string, string)>(), string.Empty);
            }

            var cards = export.Cards?
                .Select(c => (Q: c.Question ?? string.Empty, A: c.Answer ?? string.Empty))
                .ToList() ?? new List<(string, string)>();

            var progressData = export.Progress?.Enabled == true && !string.IsNullOrEmpty(export.Progress?.Data)
                ? export.Progress.Data
                : string.Empty;

            return (export.Title ?? "Imported Reviewer", cards, progressData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] JSON parse error: {ex.Message}");
            throw new InvalidDataException("Invalid JSON export file format.");
        }
    }

    /// <summary>
    /// Parse lines for "Paste Formatted .TXT" section
    /// </summary>
    static List<(string Q,string A)> ParseLines(string raw)
    {
        var result = new List<(string Q,string A)>();
        // Primary pattern: |(question:answer)| possibly multiple times
        int idx = 0;
        while (idx < raw.Length)
        {
            int start = raw.IndexOf("|(", idx, StringComparison.Ordinal);
            if (start < 0) break;
            int end = raw.IndexOf(")|", start + 2, StringComparison.Ordinal);
            if (end < 0) break;
            var inner = raw.Substring(start + 2, end - (start + 2)); // between |( and )|
            // split on first ':'
            var sep = inner.IndexOf(':');
            if (sep > 0)
            {
                var q = inner.Substring(0, sep).Trim();
                var a = inner.Substring(sep + 1).Trim();
                // Support '/n' token to denote newline in content
                if (!string.IsNullOrEmpty(q)) q = q.Replace("/n", "\n");
                if (!string.IsNullOrEmpty(a)) a = a.Replace("/n", "\n");
                if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                    result.Add((q, a));
            }
            idx = end + 2;
        }
        if (result.Count > 0) return result;

        // Fallback to legacy line formats (question | answer)
        var lines = raw.Replace("\r", string.Empty).Split('\n');
        foreach (var lineRaw in lines)
        {
            var line = lineRaw.Trim(); if (string.IsNullOrWhiteSpace(line)) continue;
            int pipeCount = line.Count(c => c == '|');
            if (pipeCount <= 0) continue;
            if (pipeCount == 1)
            {
                var idxPipe = line.IndexOf('|');
                var q = line.Substring(0, idxPipe).Trim();
                var a = line.Substring(idxPipe + 1).Trim();
                if (!string.IsNullOrEmpty(q)) q = q.Replace("/n", "\n");
                if (!string.IsNullOrEmpty(a)) a = a.Replace("/n", "\n");
                if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                    result.Add((q, a));
            }
            else
            {
                var tokens = line.Split('|');
                for (int i = 0; i + 1 < tokens.Length; i += 2)
                {
                    var q = tokens[i].Trim();
                    var a = tokens[i + 1].Trim();
                    if (!string.IsNullOrEmpty(q)) q = q.Replace("/n", "\n");
                    if (!string.IsNullOrEmpty(a)) a = a.Replace("/n", "\n");
                    if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                        result.Add((q, a));
                }
            }
        }
        if (result.Count == 0)
        {
            var tokens = raw.Split('|');
            for (int i = 0; i + 1 < tokens.Length; i += 2)
            {
                var q = tokens[i].Trim();
                var a = tokens[i + 1].Trim();
                if (!string.IsNullOrEmpty(q)) q = q.Replace("/n", "\n");
                if (!string.IsNullOrEmpty(a)) a = a.Replace("/n", "\n");
                if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                    result.Add((q, a));
            }
        }
        return result;
    }

    async void OnCreateFlashcardsTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (ProcessingIndicator != null) { ProcessingIndicator.IsVisible = true; ProcessingIndicator.IsRunning = true; }
            var raw = PasteEditor?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) { PasteResultLabel.Text = "Paste text first."; return; }
            var parsed = ParseLines(raw);
            if (parsed.Count == 0) { PasteResultLabel.Text = "No valid 'question | answer' lines."; return; }
            await CreateAndNavigateAsync(parsed);
        }
        catch (Exception ex) { PasteResultLabel.Text = $"Create Failed: {ex.Message}"; }
        finally { if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; } }
    }

    async Task CreateAndNavigateAsync(List<(string Q,string A)> parsed)
    {
        await EnsureReviewerExistsAsync();
        if (ReviewerId <= 0) { PasteResultLabel.Text = "Reviewer error."; return; }
        // Cap to maximum of 200 cards
        int total = parsed.Count;
        var capped = parsed.Take(200).ToList();
        // Replace any existing flashcards for fresh edit view
        await _db.DeleteFlashcardsForReviewerAsync(ReviewerId);
        int order = 1;
        foreach(var c in capped)
        {
            await _db.AddFlashcardAsync(new Flashcard{ ReviewerId=ReviewerId, Question=c.Q, Answer=c.A, Learned=false, Order=order++ });
        }
        _cardsAdded = true; // Cards were successfully added
        PasteResultLabel.Text = total > 200 ? $"Created 200 cards (ignored {total - 200} extra)." : $"Created {total} cards.";
        _navigatingForward = true;
        await Shell.Current.GoToAsync($"///ReviewerEditorPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
    }

    static async Task<bool> HasInternetAsync()
    {
        // Offline mode - always return false to skip internet check
        return false;
    }

    async void OnSummarize(object? sender, TappedEventArgs e)
    {
        try
        {
            // Only Windows supports local summarize AI
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            { 
                _navigatingForward = true;
                _cardsAdded = true;
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}"); 
                return; 
            }
            
            var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
            
            // Quick check if Python already exists
            if (bootstrapper.TryGetExistingPython(out var existingPath))
            {
                // Python found - go directly to SummarizeContentPage
                _navigatingForward = true;
                _cardsAdded = true;
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
                return;
            }
            
            // Python not found - check if python311.zip exists
            var zipPath = Path.Combine(AppContext.BaseDirectory, "python311.zip");
            if (!File.Exists(zipPath))
            {
                // ZIP file not found - show error
                await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                    "Setup Error",
                    "Python runtime not found. Please extract 'python311.zip' to the application folder and restart.",
                    "OK"
                ));
                return;
            }
            
            // Python not installed but ZIP exists - ask permission
            var consent = await this.ShowPopupAsync(new mindvault.Controls.AppModal(
                "AI Setup",
                "Python 3.11 is not found on your PC.\n\nWould you like to install it for offline AI features?",
                "Yes", "No"
            ));
            
            if (consent is not bool || !(bool)consent)
            {
                return; // User declined
            }
            
            // User agreed - start setup
            ShowInstallationOverlay(true, "Installing Python 3.11...", "This will only take a moment...");
            
            var progress = new Progress<string>(msg => 
            { 
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateInstallationOverlay(msg);
                });
            });
            
            try
            {
                // Extract Python and prepare environment
                await bootstrapper.EnsurePythonReadyAsync(progress, CancellationToken.None);
                
                // Verify Python was extracted successfully
                if (bootstrapper.TryGetExistingPython(out var installedPath))
                {
                    // Success - write setup flag and navigate
                    bootstrapper.WriteSetupFlag();
                    _aiEnvReady = true;
                    Preferences.Set("ai_env_ready", true);
                    
                    ShowInstallationOverlay(false);
                    
                    // Navigate directly to SummarizeContentPage
                    _navigatingForward = true;
                    _cardsAdded = true;
                    await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
                }
                else
                {
                    // Python still not found after extraction
                    ShowInstallationOverlay(false);
                    await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                        "Setup Error",
                        "Python installation failed. The application will now close.\n\nPlease restart and try again.",
                        "OK"
                    ));
                }
            }
            catch (Exception ex)
            {
                ShowInstallationOverlay(false);
                
                string userMessage;
                if (ex.Message.Contains("python311.zip"))
                {
                    userMessage = "Python 3.11 installation file not found.\n\nPlease ensure 'python311.zip' is in the application folder.";
                }
                else
                {
                    userMessage = $"Installation failed: {ex.Message}\n\nPlease restart the application and try again.";
                }
                
                await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                    "Setup Error",
                    userMessage,
                    "OK"
                ));
            }
        }
        catch (Exception ex)
        {
            ShowInstallationOverlay(false);
            await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                "Error",
                $"Setup failed: {ex.Message}",
                "OK"
            ));
        }
    }

    // Helper methods for installation overlay
    void ShowInstallationOverlay(bool show, string? statusText = null, string? detailText = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (InstallationOverlay != null)
                {
                    InstallationOverlay.IsVisible = show;
                    InstallationOverlay.InputTransparent = !show;
                }
                if (show && statusText != null && InstallationStatusLabel != null)
                {
                    InstallationStatusLabel.Text = statusText;
                }
                if (show && detailText != null && InstallationDetailLabel != null)
                {
                    InstallationDetailLabel.Text = detailText;
                }
                if (!show && InstallationSpinner != null)
                {
                    InstallationSpinner.IsRunning = false;
                }
                else if (show && InstallationSpinner != null)
                {
                    InstallationSpinner.IsRunning = true;
                }
            }
            catch { }
        });
    }

    void ShowLoadingSpinner(bool show, string? statusText = null, string? detailText = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (InstallationOverlay != null)
                {
                    InstallationOverlay.IsVisible = show;
                    InstallationOverlay.InputTransparent = !show;
                }
                if (show && statusText != null && InstallationStatusLabel != null)
                {
                    InstallationStatusLabel.Text = statusText;
                }
                if (show && detailText != null && InstallationDetailLabel != null)
                {
                    InstallationDetailLabel.Text = detailText;
                }
                else if (show && InstallationDetailLabel != null)
                {
                    InstallationDetailLabel.Text = "This will only take a moment...";
                }
                if (!show && InstallationSpinner != null)
                {
                    InstallationSpinner.IsRunning = false;
                }
                else if (show && InstallationSpinner != null)
                {
                    InstallationSpinner.IsRunning = true;
                }
            }
            catch { }
        });
    }

    void UpdateInstallationOverlay(string progressMessage)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (InstallationStatusLabel != null)
                {
                    // Parse progress messages to create user-friendly status
                    if (progressMessage.Contains("Installing Python"))
                    {
                        InstallationStatusLabel.Text = "Installing Python...";
                        InstallationDetailLabel.Text = "Please wait while we install Python automatically";
                    }
                    else if (progressMessage.Contains("Downloading"))
                    {
                        InstallationStatusLabel.Text = "Downloading...";
                        InstallationDetailLabel.Text = progressMessage;
                    }
                    else if (progressMessage.Contains("llama"))
                    {
                        InstallationStatusLabel.Text = "Building llama-cpp-python...";
                        InstallationDetailLabel.Text = "This may take several minutes";
                    }
                    else if (progressMessage.Contains("wheel"))
                    {
                        InstallationStatusLabel.Text = "Installing AI components...";
                        InstallationDetailLabel.Text = progressMessage;
                    }
                    else if (progressMessage.Contains("pip"))
                    {
                        InstallationStatusLabel.Text = "Setting up Python packages...";
                        InstallationDetailLabel.Text = progressMessage;
                    }
                    else if (progressMessage.Contains("Waiting for PATH"))
                    {
                        InstallationStatusLabel.Text = "Detecting Python installation...";
                        InstallationDetailLabel.Text = progressMessage;
                    }
                    else
                    {
                        InstallationStatusLabel.Text = progressMessage;
                    }
                }
            }
            catch { }
        });
    }
}
