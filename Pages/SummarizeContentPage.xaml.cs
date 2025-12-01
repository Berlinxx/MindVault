using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using System.Text;
using mindvault.Services; // still needed for ServiceHelper/DatabaseService
using mindvault.Utils;
using mindvault.Data;
using Microsoft.Maui.Storage;
using mindvault.Models;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using System.Text.RegularExpressions; // added for cleaning

namespace mindvault.Pages;

[QueryProperty(nameof(ReviewerId), "id")]
[QueryProperty(nameof(ReviewerTitle), "title")]
public partial class SummarizeContentPage : ContentPage
{
    int _reviewerId;
    public int ReviewerId 
    { 
        get => _reviewerId;
        set 
        { 
            _reviewerId = value;
        }
    }
    
    string _reviewerTitle = string.Empty;
    public string ReviewerTitle 
    { 
        get => _reviewerTitle;
        set 
        { 
            _reviewerTitle = value ?? string.Empty;
            // Update UI when title is set via query parameter
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (DeckTitleLabel != null)
                {
                    DeckTitleLabel.Text = _reviewerTitle;
                }
            });
        }
    }

    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();
    readonly FileTextExtractor _extractor = ServiceHelper.GetRequiredService<FileTextExtractor>();
    readonly PythonFlashcardService _py = ServiceHelper.GetRequiredService<PythonFlashcardService>();

    string _rawContent = string.Empty;
    CancellationTokenSource? _genCts;
    bool _isWindows = DeviceInfo.Platform == DevicePlatform.WinUI;
    bool _envChecked = false;

    // Progress state
    int _totalChunks = 0;
    int _currentChunk = 0;
    DateTime _startTime;
    double _overlayProgressValue = 0.0;
    DateTime _lastProgressUpdate = DateTime.MinValue;
    const int PROGRESS_THROTTLE_MS = 500; // Update UI at most every 500ms to reduce load

    public SummarizeContentPage()
    {
        InitializeComponent();
        ContentEditor.TextChanged += OnEditorChanged;
        // react to track size changes so fills resize correctly
        OverlayTrack.SizeChanged += (s, e) => SetOverlayProgress(_overlayProgressValue);
        if (!_isWindows)
        {
            GenerateButton.IsVisible = false;
            StatusLabel.Text = "Flashcard generation is available on PC only.";
        }
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        DeckTitleLabel.Text = ReviewerTitle;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mindvault.Utils.AnimHelpers.SlideFadeInAsync(Content);
        
        if (_isWindows)
        {
            // Always recheck environment when page appears
            _envChecked = false; // Reset flag to force recheck
            
            // Quick initial check
            await QuickCheckEnvironmentAsync();
            
            // Also update based on current content
            if (!string.IsNullOrWhiteSpace(_rawContent))
            {
                await UpdateButtonVisibilityAsync();
            }
        }
    }

    void OnEditorChanged(object? sender, TextChangedEventArgs e)
    {
        _rawContent = e.NewTextValue ?? string.Empty;
        if (_isWindows)
        {
            // Update button visibility when content changes
            _ = Task.Run(async () =>
            {
                try
                {
                    await UpdateButtonVisibilityAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SummarizeContent] OnEditorChanged error: {ex.Message}");
                }
            });
        }
    }

    async Task UpdateButtonVisibilityAsync()
    {
        try
        {
            var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
            var healthy = await bootstrapper.IsEnvironmentHealthyAsync();
            var hasContent = !string.IsNullOrWhiteSpace(_rawContent);
            
            Debug.WriteLine($"[SummarizeContent] UpdateButtonVisibilityAsync: healthy={healthy}, hasContent={hasContent}");
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GenerateButton.IsVisible = healthy && hasContent;
                ManualInstallButton.IsVisible = !healthy;
                
                // Update status text to guide user
                if (!healthy)
                {
                    StatusLabel.Text = "Setup incomplete. Please use AI Summarize button on previous page.";
                }
                else if (!hasContent)
                {
                    StatusLabel.Text = "Paste or upload content to generate flashcards";
                }
                else
                {
                    StatusLabel.Text = "Ready to generate flashcards";
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SummarizeContent] UpdateButtonVisibilityAsync exception: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GenerateButton.IsVisible = false;
                ManualInstallButton.IsVisible = true;
                StatusLabel.Text = $"Check failed: {ex.Message}";
            });
        }
    }

    async void OnBack(object? sender, TappedEventArgs e) => await Navigation.PopAsync();
    async void OnClose(object? sender, TappedEventArgs e)
    {
        var route = $"///AddFlashcardsPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}";
        try { await Shell.Current.GoToAsync(route); } catch { await Navigation.PopAsync(); }
    }

    // Cleaning helper
    static string CleanInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Length < 10 && (line.Equals("endobj", StringComparison.OrdinalIgnoreCase) || line.Equals("xref", StringComparison.OrdinalIgnoreCase))) continue;
            if (Regex.IsMatch(line, "^Page\\s+\\d+(?:\\s+of\\s+\\d+)?$", RegexOptions.IgnoreCase)) continue;
            if (Regex.IsMatch(line, @"^(?:[=\u2014\-]{3,}|[*/]{3,})$")) continue; // repeated separators
            if (Regex.IsMatch(line, @"^/\w+")) continue;
            line = Regex.Replace(line, @"\s+", " ");
            line = new string(line.Where(ch => !char.IsControl(ch)).ToArray());
            sb.Append(line.Trim()).Append(' ');
        }
        var cleaned = sb.ToString();
        cleaned = Regex.Replace(cleaned, @"(?<=\w)-\n(?=\w)", ""); // likely no \n left but safe
        cleaned = cleaned.Replace('\n', ' '); // flatten any residual newlines
        cleaned = Regex.Replace(cleaned, @"\s+", " "); // collapse whitespace again
        return cleaned.Trim();
    }

    async void OnUploadFile(object? sender, TappedEventArgs e)
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[]{"application/pdf","application/vnd.openxmlformats-officedocument.wordprocessingml.document","application/vnd.openxmlformats-officedocument.presentationml.presentation","text/plain"} },
                { DevicePlatform.iOS, new[]{"com.adobe.pdf","org.openxmlformats.wordprocessingml.document","org.openxmlformats.presentationml.presentation","public.plain-text"} },
                { DevicePlatform.MacCatalyst, new[]{"com.adobe.pdf","org.openxmlformats.wordprocessingml.document","org.openxmlformats.presentationml.presentation","public.plain-text"} },
                { DevicePlatform.WinUI, new[]{".pdf",".docx",".pptx",".txt"} },
            });
            var pick = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select Lesson File", FileTypes = fileTypes });
            if (pick == null) return;
            StatusLabel.Text = "Extracting text...";
            var text = await _extractor.ExtractAsync(pick);
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusLabel.Text = "Could not read text from file.";
                return;
            }
            var cleaned = CleanInput(text);
            ContentEditor.Text = cleaned;
            StatusLabel.Text = $"Loaded {cleaned.Length} chars (cleaned).";
        }
        catch (Exception ex)
        {
            this.ShowPopup(new AppModal("File", ex.Message, "OK"));
        }
    }

    async Task QuickCheckEnvironmentAsync()
    {
        try
        {
            var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
            // Fast check only - all heavy lifting done in AddFlashcardsPage
            bool ready = await bootstrapper.QuickSystemPythonHasLlamaAsync();
            
            Debug.WriteLine($"[SummarizeContent] QuickCheckEnvironmentAsync: ready={ready}");
            
            if (ready)
            {
                // Environment is ready - show generate button
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ManualInstallButton.IsVisible = false;
                    GenerateButton.IsVisible = !string.IsNullOrWhiteSpace(_rawContent);
                    StatusLabel.Text = "Ready to generate flashcards";
                });
            }
            else
            {
                // Environment not ready - hide both buttons and show message
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = "Python + llama required. Use the AI Summarize button on the previous page to install.";
                    ManualInstallButton.IsVisible = false;
                    GenerateButton.IsVisible = false;
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SummarizeContent] QuickCheckEnvironmentAsync exception: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "Environment check failed: " + ex.Message;
                ManualInstallButton.IsVisible = false;
                GenerateButton.IsVisible = false;
            });
        }
    }

    async void OnManualInstall(object? sender, TappedEventArgs e)
    {
        // This button should now be hidden - redirect to AddFlashcardsPage for installation
        await this.ShowPopupAsync(new AppModal(
            "Installation Required",
            "Please return to the previous page and use the AI Summarize button to complete the installation.",
            "OK"
        ));
    }

    async void OnGenerate(object? sender, TappedEventArgs e)
    {
        if (!_isWindows) { StatusLabel.Text = "PC only"; return; }
        if (string.IsNullOrWhiteSpace(_rawContent)) return;
        _genCts?.Cancel();
        _genCts = new CancellationTokenSource();
        try
        {
            var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
            if (!await bootstrapper.IsEnvironmentHealthyAsync())
            {
                StatusLabel.Text = "Python/llama not ready. Use Install button above.";
                ManualInstallButton.IsVisible = true;
                GenerateButton.IsVisible = false;
                return;
            }
            _startTime = DateTime.UtcNow;
            _totalChunks = 0; _currentChunk = 0;
            ShowLoading(true);
            StatusLabel.Text = "Preparing Python...";
            GenerateButton.IsVisible = false;
            ContentEditor.IsEnabled = false;

            // Ensure UI has time to update before starting heavy processing
            await Task.Delay(50);

            var progress = new Progress<string>(p => UpdateProgressFromPython(p));
            
            // Run the generation on a background thread to keep UI responsive
            var cards = await Task.Run(() => _py.GenerateAsync(_rawContent, progress, _genCts.Token), _genCts.Token);
            
            ContentEditor.IsEnabled = true;
            ShowLoading(false);
            
            if (cards.Count == 0)
            {
                StatusLabel.Text = "No term definitions detected.";
                GenerateButton.IsVisible = true;
                return;
            }
            
            // Process results on UI thread
            App.GeneratedFlashcards.Clear();
            int order = 1;
            
            // Batch database operations to reduce UI blocking
            var flashcardsToAdd = new List<Flashcard>();
            foreach (var c in cards)
            {
                App.GeneratedFlashcards.Add(c);
                flashcardsToAdd.Add(new Flashcard
                {
                    ReviewerId = ReviewerId,
                    Question = c.Question,
                    Answer = c.Answer,
                    Learned = false,
                    Order = order++
                });
            }
            
            // Add all flashcards in background
            await Task.Run(async () =>
            {
                foreach (var flashcard in flashcardsToAdd)
                {
                    await _db.AddFlashcardAsync(flashcard);
                }
            });
            
            StatusLabel.Text = $"Added {cards.Count} cards.";
            await Task.Delay(300);
            var route = $"///ReviewerEditorPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}";
            await Shell.Current.GoToAsync(route);
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Generation canceled.";
            ContentEditor.IsEnabled = true;
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[Python] Error: " + ex);
            StatusLabel.Text = ex.Message;
            GenerateButton.IsVisible = true;
            ContentEditor.IsEnabled = true;
            ShowLoading(false);
            try
            {
                var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
                StatusLabel.Text += $" (See log: {bootstrapper.LogPath})";
            }
            catch { }
        }
    }

    void UpdateProgressFromPython(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;
        
        // Throttle UI updates to prevent blocking
        var now = DateTime.UtcNow;
        bool shouldThrottle = (now - _lastProgressUpdate).TotalMilliseconds < PROGRESS_THROTTLE_MS;
        
        if (msg.StartsWith("::TOTAL::"))
        {
            var tail = msg.Substring("::TOTAL::".Length);
            if (int.TryParse(tail, out _totalChunks))
            {
                _currentChunk = 0;
                _lastProgressUpdate = now;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetOverlayProgress(0);
                    UpdateOverlayText();
                });
            }
        }
        else if (msg.StartsWith("::CHUNK::"))
        {
            var parts = msg.Split("::", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[1], out var idx) && int.TryParse(parts[2], out var total))
            {
                _currentChunk = idx; 
                _totalChunks = total;
                
                // Only update UI if enough time has passed (throttling)
                if (!shouldThrottle || idx == total) // Always update on last chunk
                {
                    _lastProgressUpdate = now;
                    var op = (_totalChunks == 0) ? 0 : Math.Min(1.0, (double)idx / _totalChunks);
                    
                    // Use BeginInvokeOnMainThread to avoid blocking
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetOverlayProgress(op);
                        UpdateOverlayText();
                    });
                }
            }
        }
        else if (msg.StartsWith("::DONE::"))
        {
            _lastProgressUpdate = now;
            // finish overlay
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetOverlayProgress(1.0);
                OverlayProgressLabel.Text = "Finalizing output...";
            });
        }
        else
        {
            // Status messages are less frequent, so always update
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = msg;
            });
        }
    }

    void UpdateOverlayText()
    {
        if (_totalChunks <= 0) { OverlayProgressLabel.Text = "Starting..."; return; }
        var elapsed = DateTime.UtcNow - _startTime;
        var done = Math.Max(0, _currentChunk - 1);
        double per = done <= 0 ? 0 : elapsed.TotalSeconds / done;
        var remaining = _totalChunks - done;
        var eta = TimeSpan.FromSeconds(Math.Max(0, per * remaining));
        var etaText = eta.TotalSeconds < 1 ? "<1s" : (eta.TotalMinutes >= 1 ? $"~{(int)eta.TotalMinutes}m {eta.Seconds}s" : $"~{eta.Seconds}s");
        OverlayProgressLabel.Text = $"Processing chunk {_currentChunk}/{_totalChunks}  ETA {etaText}";
    }

    void ShowLoading(bool show)
    {
        // Use BeginInvokeOnMainThread to avoid blocking
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingOverlay.IsVisible = show;
            
            if (show)
            {
                _overlayProgressValue = 0;
                _lastProgressUpdate = DateTime.MinValue; // Reset throttle
                if (OverlayFill != null) OverlayFill.WidthRequest = 0;
                OverlayProgressLabel.Text = "This might take some time...";
            }
            else
            {
                // stop shimmer animation
                try { this.AbortAnimation("ShimmerOverlay"); } catch { }
            }
        });
    }

    // Inline visual replaced with a spinner + value label; old inline progress removed.

    void SetOverlayProgress(double progress)
    {
        _overlayProgressValue = progress;
        
        // Don't dispatch if already on main thread
        if (MainThread.IsMainThread)
        {
            UpdateProgressUI(progress);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateProgressUI(progress));
        }
    }

    void UpdateProgressUI(double progress)
    {
        if (OverlayTrack == null || OverlayFill == null) return;
        
        var trackWidth = Math.Max(0, OverlayTrack.Width - (OverlayTrack.Padding.Left + OverlayTrack.Padding.Right));
        var target = trackWidth * progress;
        
        // Skip animation if width change is very small to reduce UI workload
        var currentWidth = OverlayFill.WidthRequest;
        if (Math.Abs(currentWidth - target) < 5 && progress < 1.0) // Increased threshold
        {
            return;
        }
        
        // Directly set width instead of animating to reduce UI load
        OverlayFill.WidthRequest = target;
        
        // Disable shimmer animation during processing to prevent UI blocking
        // if (progress > 0 && progress < 1)
        //     StartShimmer(OverlayShimmer, OverlayTrack, "ShimmerOverlay");
        // else
        //     this.AbortAnimation("ShimmerOverlay");
    }

    void StartShimmer(VisualElement shimmer, VisualElement track, string animName)
    {
        if (shimmer == null || track == null) return;
        // stop any existing shimmer with the same name
        try { this.AbortAnimation(animName); } catch { }
        // ensure shimmer is visible
        shimmer.IsVisible = true;
        // compute bounds
        var trackWidth = Math.Max(0, track.Width - (track is Border b ? (b.Padding.Left + b.Padding.Right) : 0));
        var shimmerWidth = shimmer.Width <= 0 ? (shimmer is BoxView bv ? bv.WidthRequest : 80) : shimmer.Width;
        var from = -shimmerWidth;
        var to = trackWidth + shimmerWidth;
        var duration = 900u;
        void loop()
        {
            var a = new Animation(p => shimmer.TranslationX = p, from, to, Easing.Linear);
            a.Commit(this, animName, length: duration, finished: (v, c) =>
            {
                // restart while overlay progress not complete
                if (_overlayProgressValue < 1.0)
                {
                    loop();
                }
                else
                {
                    shimmer.TranslationX = 0;
                }
            });
        }
        loop();
    }

    static string Truncate(string s, int len) => s.Length <= len ? s : s.Substring(0, len).Trim() + "...";
}
