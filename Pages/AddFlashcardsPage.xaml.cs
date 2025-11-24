using mindvault.Services;
using mindvault.Utils;
using mindvault.Data;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls; // ensure ContentPage & TappedEventArgs
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
    public int ReviewerId { get; set; }
    public string ReviewerTitle { get; set; } = string.Empty;
    readonly DatabaseService _db = ServiceHelper.GetRequiredService<DatabaseService>();
    bool _navigatingForward;
    bool _aiEnvReady = false; // track environment readiness

    public AddFlashcardsPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    { base.OnNavigatedTo(args); DeckTitleLabel.Text = $"Deck: {ReviewerTitle}"; }

    protected override async void OnAppearing()
    { 
        base.OnAppearing(); 
        await AnimHelpers.SlideFadeInAsync(Content); 
        _navigatingForward = false;
        // Prefill editor with format examples if user hasn't typed anything yet
        try
        {
            if (PasteEditor != null && string.IsNullOrWhiteSpace(PasteEditor.Text))
            {
                PasteEditor.Text = "|(An array of components designed to accomplish a particular objective according to plan.:System)|\n|(A way of understanding an entity in terms of its purpose, as three steps.:Systems Thinking)|\n";
            }
        }
        catch { }
        // load environment flag
        _aiEnvReady = Preferences.Get("ai_env_ready", false);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        // Do not auto-delete here anymore; user will review in editor.
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

    async void OnTypeFlashcards(object? s, TappedEventArgs e)
    { _navigatingForward = true; await Shell.Current.GoToAsync($"///ReviewerEditorPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}"); }

    async void OnImportPaste(object? s, TappedEventArgs e)
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "text/plain" } },
                { DevicePlatform.iOS, new[] { "public.plain-text" } },
                { DevicePlatform.MacCatalyst, new[] { "public.plain-text" } },
                { DevicePlatform.WinUI, new[] { ".txt" } },
            });
            var pick = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select export file (.txt)", FileTypes = fileTypes });
            if (pick is null) return;
            if (!string.Equals(Path.GetExtension(pick.FileName), ".txt", StringComparison.OrdinalIgnoreCase)) { await DisplayAlert("Import", "Only .txt files supported.", "OK"); return; }
            string content; using (var stream = await pick.OpenReadAsync()) using (var reader = new StreamReader(stream)) content = await reader.ReadToEndAsync();
            var (_, parsed) = ParseExport(content);
            await CreateAndNavigateAsync(parsed);
        }
        catch (Exception ex) { await DisplayAlert("Import Failed", ex.Message, "OK"); }
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
                if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                    result.Add((q, a));
            }
        }
        return result;
    }

    static (string Title, List<(string Q, string A)> Cards) ParseExport(string content)
    {
        var lines = content.Replace("\r", string.Empty).Split('\n');
        string title = lines.FirstOrDefault(l => l.StartsWith("Reviewer:", StringComparison.OrdinalIgnoreCase))?.Substring(9).Trim() ?? "Imported";
        var cards = new List<(string Q, string A)>();
        string? q = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("Q:", StringComparison.OrdinalIgnoreCase)) { if (!string.IsNullOrWhiteSpace(q)) cards.Add((q, string.Empty)); q = line.Substring(2).Trim(); }
            else if (line.StartsWith("A:", StringComparison.OrdinalIgnoreCase)) { var a = line.Substring(2).Trim(); if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a)) { cards.Add((q ?? string.Empty, a)); q = null; } }
        }
        if (!string.IsNullOrWhiteSpace(q)) cards.Add((q, string.Empty));
        return (title, cards);
    }

    async void OnCreateFlashcardsTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            var raw = PasteEditor?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) { PasteResultLabel.Text = "Paste text first."; return; }
            var parsed = ParseLines(raw);
            if (parsed.Count == 0) { PasteResultLabel.Text = "No valid 'question | answer' lines."; return; }
            await CreateAndNavigateAsync(parsed);
        }
        catch (Exception ex) { PasteResultLabel.Text = $"Create Failed: {ex.Message}"; }
    }

    async Task CreateAndNavigateAsync(List<(string Q,string A)> parsed)
    {
        await EnsureReviewerExistsAsync();
        if (ReviewerId <= 0) { PasteResultLabel.Text = "Reviewer error."; return; }
        // Replace any existing flashcards for fresh edit view
        await _db.DeleteFlashcardsForReviewerAsync(ReviewerId);
        int order = 1;
        foreach(var c in parsed)
        {
            await _db.AddFlashcardAsync(new Flashcard{ ReviewerId=ReviewerId, Question=c.Q, Answer=c.A, Learned=false, Order=order++ });
        }
        PasteResultLabel.Text = $"Created {parsed.Count} cards.";
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
        // Only Windows supports local summarize AI
        if (DeviceInfo.Platform != DevicePlatform.WinUI)
        { _navigatingForward = true; await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}"); return; }
        if (!_aiEnvReady)
        {
            var bootstrapper = ServiceHelper.GetRequiredService<PythonBootstrapper>();
            // New: fast system detection first
            if (await bootstrapper.QuickSystemPythonHasLlamaAsync())
            {
                _aiEnvReady = true; Preferences.Set("ai_env_ready", true);
                _navigatingForward = true;
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
                return;
            }
            if (await bootstrapper.IsEnvironmentHealthyAsync())
            {
                _aiEnvReady = true; Preferences.Set("ai_env_ready", true);
                _navigatingForward = true;
                await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
                return;
            }
            var consent = await DisplayAlert("Local AI Setup","Install / verify Python + llama-cpp-python model now? (~50-150MB download)","Install","Cancel");
            if (!consent) return;
            // Internet check
            var online = await HasInternetAsync();
            if (!online)
            {
                var msg = "MindVault needs an internet connection to download Python and required dependencies. This is a one-time setup. Please connect to the internet and try again.";
                PasteResultLabel.Text = "?? " + msg;
                try { System.IO.File.AppendAllText(bootstrapper.LogPath, msg + "\n"); } catch { }
                return;
            }
            try
            {
                PasteResultLabel.Text = "Preparing environment...";
                var progress = new Progress<string>(msg => { PasteResultLabel.Text = msg; });
                await bootstrapper.EnsurePythonReadyAsync(progress, CancellationToken.None);
                _aiEnvReady = true;
                Preferences.Set("ai_env_ready", true);
                PasteResultLabel.Text = "Environment ready.";
            }
            catch (Exception ex)
            {
                PasteResultLabel.Text = "Setup failed: " + ex.Message;
                return;
            }
        }
        _navigatingForward = true;
        await Shell.Current.GoToAsync($"///SummarizeContentPage?id={ReviewerId}&title={Uri.EscapeDataString(ReviewerTitle)}");
    }
}
