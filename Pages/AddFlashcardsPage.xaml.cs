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
            
            if (pick is null) 
            {
                if (ProcessingIndicator != null) { ProcessingIndicator.IsRunning = false; ProcessingIndicator.IsVisible = false; }
                return;
            }
            
            if (!string.Equals(Path.GetExtension(pick.FileName), ".txt", StringComparison.OrdinalIgnoreCase)) 
            { 
                await DisplayAlert("Import", "Only .txt files are supported.", "OK"); 
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
            
            var (importedTitle, parsed, progressData) = ParseExport(content);
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
                    "Continue Progress", "Start Fresh"));
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
                    var bytes = Convert.FromBase64String(progressData);
                    var progressJson = System.Text.Encoding.UTF8.GetString(bytes);
                    
                    System.Diagnostics.Debug.WriteLine($"[AddFlashcardsPage] Original progress JSON length: {progressJson.Length}");
                    
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

    static (string Title, List<(string Q, string A)> Cards, string ProgressData) ParseExport(string content)
    {
        var lines = content.Replace("\r", string.Empty).Split('\n');
        string title = lines.FirstOrDefault(l => l.StartsWith("Reviewer:", StringComparison.OrdinalIgnoreCase))?.Substring(9).Trim() ?? "Imported";
        string progressData = string.Empty;
        
        // Check for progress data
        var progressLine = lines.FirstOrDefault(l => l.StartsWith("ProgressData:", StringComparison.OrdinalIgnoreCase));
        if (progressLine != null)
        {
            progressData = progressLine.Substring(13).Trim();
        }
        
        var cards = new List<(string Q, string A)>();
        string? q = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            // Skip metadata lines
            if (line.StartsWith("Reviewer:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Questions:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Progress:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("ProgressData:", StringComparison.OrdinalIgnoreCase))
                continue;
                
            if (line.StartsWith("Q:", StringComparison.OrdinalIgnoreCase)) { if (!string.IsNullOrWhiteSpace(q)) cards.Add((q, string.Empty)); q = line.Substring(2).Trim(); }
            else if (line.StartsWith("A:", StringComparison.OrdinalIgnoreCase)) { var a = line.Substring(2).Trim(); if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a)) { cards.Add((q ?? string.Empty, a)); q = null; } }
        }
        if (!string.IsNullOrWhiteSpace(q)) cards.Add((q, string.Empty));
        return (title, cards, progressData);
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
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            // lightweight HEAD attempt
            var req = new HttpRequestMessage(HttpMethod.Head, "https://www.python.org/");
            var resp = await client.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    async void OnSummarize(object? sender, TappedEventArgs e)
    {
        try
        {
            // Only Windows supports local summarize AI
            if (DeviceInfo.Platform != DevicePlatform.WinUI)
            { 
                _navigatingForward = true;
                _cardsAdded = true; // User is going to AI summarize (will add cards)
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}"); 
                return; 
            }
            
            // Show loading spinner during environment check
            ShowLoadingSpinner(true, "Checking AI environment...", "Verifying Python and dependencies...");
            
            // ALWAYS revalidate environment (ignore _aiEnvReady cache) to detect uninstalled Python
            var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
            
            // New: fast system detection first
            if (await bootstrapper.QuickSystemPythonHasLlamaAsync())
            {
                ShowLoadingSpinner(false);
                _aiEnvReady = true; 
                Preferences.Set("ai_env_ready", true);
                _navigatingForward = true;
                _cardsAdded = true; // User is going to AI summarize (will add cards)
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
                return;
            }
            if (await bootstrapper.IsEnvironmentHealthyAsync())
            {
                ShowLoadingSpinner(false);
                _aiEnvReady = true; 
                Preferences.Set("ai_env_ready", true);
                _navigatingForward = true;
                _cardsAdded = true; // User is going to AI summarize (will add cards)
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
                return;
            }
            
            // Hide loading spinner before showing consent dialog
            ShowLoadingSpinner(false);
            
            // Environment NOT healthy - clear cache and trigger install
            _aiEnvReady = false;
            Preferences.Set("ai_env_ready", false);
            
            AiSetupStatusLabel.Text = "Asking for installation consent...";
            
            // Combined modal: Install consent with automatic installation info
            var consentResult = await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                "Local AI Setup",
                "Python or llama-cpp-python not detected. Install now?\n\n" +
                "(~50-150MB download)\n\n" +
                "The installation will run automatically in the background.\n\n" +
                "This is a one-time setup and may take 1-3 minutes.\n\n" +
                "Python and AI components will be configured automatically.",
                "Install",
                "Cancel"
            ));
            
            var consent = consentResult is bool b && b;
            
            AiSetupStatusLabel.Text = $"User response: {(consent ? "Install" : "Cancel")}";
            
            
            if (!consent) 
            {
                AiSetupStatusLabel.Text = "Installation cancelled by user.";
                return;
            }
            
            // Show overlay immediately after consent
            ShowInstallationOverlay(true, "Checking internet connection...", "Please wait...");
            
            AiSetupStatusLabel.Text = "User confirmed. Checking internet...";
            
            // Internet check
            var online = await HasInternetAsync();
            if (!online)
            {
                ShowInstallationOverlay(false);
                var msg = "MindVault needs an internet connection to download Python and required dependencies. This is a one-time setup. Please connect to the internet and try again.";
                AiSetupStatusLabel.Text = "?? " + msg;
                try { System.IO.File.AppendAllText(bootstrapper.LogPath, msg + "\n"); } catch { }
                return;
            }
            
            ShowInstallationOverlay(true, "Installing Python...", "Downloading installer, please wait...");
            AiSetupStatusLabel.Text = "Preparing environment...";
            
            var progress = new Progress<string>(msg => 
            { 
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AiSetupStatusLabel.Text = msg;
                    UpdateInstallationOverlay(msg);
                });
            });
            
            // Ensure Python is installed first
            ((IProgress<string>)progress).Report("Installing Python...");
            await bootstrapper.EnsurePythonReadyAsync(progress, CancellationToken.None);
            
            // Check if llama was already installed during EnsurePythonReadyAsync
            bool llamaAlreadyInstalled = await bootstrapper.IsLlamaAvailableAsync();
            
            if (!llamaAlreadyInstalled)
            {
                // Build llama in visible CMD window - spinner stays visible
                ShowInstallationOverlay(true, "Building llama-cpp-python...", "A CMD window will open. Please wait for it to close automatically.");
                AiSetupStatusLabel.Text = "Building llama-cpp-python in CMD...";
                
                await bootstrapper.BuildLlamaInCmdAsync(progress, CancellationToken.None);
            }
            
            // Verify installation succeeded (including llama)
            ShowInstallationOverlay(true, "Verifying installation...", "Almost done...");
            AiSetupStatusLabel.Text = "Verifying llama-cpp-python...";
            
            if (await bootstrapper.IsEnvironmentHealthyAsync())
            {
                _aiEnvReady = true;
                Preferences.Set("ai_env_ready", true);
                AiSetupStatusLabel.Text = "? Environment ready (Python + llama-cpp-python).";
                ShowInstallationOverlay(false);
            }
            else
            {
                _aiEnvReady = false;
                Preferences.Set("ai_env_ready", false);
                ShowInstallationOverlay(false);
                
                // Show user-friendly error modal
                await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                    "Setup Incomplete",
                    "Python was installed but llama-cpp-python failed to build. This may require Visual Studio Build Tools. Please restart and try again.",
                    "OK"
                ));
                
                AiSetupStatusLabel.Text = "?? Setup incomplete - llama build failed";
                return;
            }
            
            _navigatingForward = true;
            _cardsAdded = true; // User is going to AI summarize (will add cards)
            await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
        }
        catch (Exception ex)
        {
            ShowInstallationOverlay(false);
            _aiEnvReady = false;
            Preferences.Set("ai_env_ready", false);
            
            // Create user-friendly error message
            string userMessage;
            if (ex.Message.Contains("Python") || ex.Message.Contains("python"))
            {
                userMessage = "Python installation failed. Please install Python 3.11 manually from python.org and ensure 'Add to PATH' is checked.";
            }
            else if (ex.Message.Contains("llama") || ex.Message.Contains("pip"))
            {
                userMessage = "Failed to install AI dependencies. Please check your internet connection and try again.";
            }
            else if (ex.Message.Contains("internet") || ex.Message.Contains("network") || ex.Message.Contains("connection"))
            {
                userMessage = "Network error. Please check your internet connection and try again.";
            }
            else
            {
                userMessage = "Setup failed. Please try again or install Python 3.11 manually from python.org.";
            }
            
            AiSetupStatusLabel.Text = "?? Setup failed";
            
            // Show user-friendly error modal
            await this.ShowPopupAsync(new mindvault.Controls.InfoModal(
                "Setup Error",
                userMessage,
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
