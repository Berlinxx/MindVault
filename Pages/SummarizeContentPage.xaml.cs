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
    public int ReviewerId { get; set; }
    public string ReviewerTitle { get; set; } = string.Empty;

    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();
    readonly FileTextExtractor _extractor = ServiceHelper.GetRequiredService<FileTextExtractor>();
    readonly PythonFlashcardService _py = ServiceHelper.GetRequiredService<PythonFlashcardService>();

    string _rawContent = string.Empty;
    CancellationTokenSource? _genCts;
    bool _isWindows = DeviceInfo.Platform == DevicePlatform.WinUI;

    // Progress state
    int _totalChunks = 0;
    int _currentChunk = 0;
    DateTime _startTime;

    public SummarizeContentPage()
    {
        InitializeComponent();
        ContentEditor.TextChanged += OnEditorChanged;
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
    }

    void OnEditorChanged(object? sender, TextChangedEventArgs e)
    {
        _rawContent = e.NewTextValue ?? string.Empty;
        if (_isWindows)
            GenerateButton.IsVisible = !string.IsNullOrWhiteSpace(_rawContent);
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
            await DisplayAlert("File", ex.Message, "OK");
        }
    }

    async void OnGenerate(object? sender, TappedEventArgs e)
    {
        if (!_isWindows) { StatusLabel.Text = "PC only"; return; }
        if (string.IsNullOrWhiteSpace(_rawContent)) return;
        _genCts?.Cancel();
        _genCts = new CancellationTokenSource();
        try
        {
            _startTime = DateTime.UtcNow;
            _totalChunks = 0; _currentChunk = 0;
            ShowLoading(true);
            StatusLabel.Text = "Preparing Python...";
            GenerateButton.IsVisible = false;
            ContentEditor.IsEnabled = false;

            var progress = new Progress<string>(p => UpdateProgressFromPython(p));
            var cards = await Task.Run(() => _py.GenerateAsync(_rawContent, progress, _genCts.Token));
            ContentEditor.IsEnabled = true;
            ShowLoading(false);
            if (cards.Count == 0)
            {
                StatusLabel.Text = "No term definitions detected.";
                GenerateButton.IsVisible = true;
                return;
            }
            App.GeneratedFlashcards.Clear();
            int order = 1;
            foreach (var c in cards)
            {
                App.GeneratedFlashcards.Add(c);
                await _db.AddFlashcardAsync(new Flashcard
                {
                    ReviewerId = ReviewerId,
                    Question = c.Question,
                    Answer = c.Answer,
                    Learned = false,
                    Order = order++
                });
            }
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
        if (msg.StartsWith("::TOTAL::"))
        {
            var tail = msg.Substring("::TOTAL::".Length);
            if (int.TryParse(tail, out _totalChunks))
            {
                _currentChunk = 0;
                InlineProgress.IsVisible = _totalChunks > 0;
                ChunkProgress.Progress = 0;
                ChunkLabel.Text = $"0 / {_totalChunks}";
                UpdateOverlayText();
            }
        }
        else if (msg.StartsWith("::CHUNK::"))
        {
            var parts = msg.Split("::", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[1], out var idx) && int.TryParse(parts[2], out var total))
            {
                _currentChunk = idx; _totalChunks = total;
                InlineProgress.IsVisible = true;
                ChunkProgress.Progress = (_totalChunks == 0) ? 0 : Math.Min(1.0, (double)(idx-1) / _totalChunks);
                ChunkLabel.Text = $"{idx} / {total}";
                UpdateOverlayText();
            }
        }
        else if (msg.StartsWith("::DONE::"))
        {
            ChunkProgress.Progress = 1.0;
            ChunkLabel.Text = $"Done ({_currentChunk} / {_totalChunks})";
            OverlayProgressLabel.Text = "Finalizing output...";
        }
        else
        {
            StatusLabel.Text = msg;
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
        LoadingOverlay.IsVisible = show;
        LoadingSpinner.IsRunning = show;
    }

    static string Truncate(string s, int len) => s.Length <= len ? s : s.Substring(0, len).Trim() + "...";
}
