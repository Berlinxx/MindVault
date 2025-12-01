using mindvault.Services;
using mindvault.Utils;
using System.Diagnostics;
using mindvault.Data;
using Microsoft.Maui.Storage;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using mindvault.Models;

namespace mindvault.Pages;

public partial class ExportPage : ContentPage
{
    public string ReviewerTitle { get; }
    public int Questions => Cards?.Count ?? 0;
    public string QuestionsText => Questions.ToString();

    // Bindable collection with property names
    public ObservableCollection<CardPreview> Cards { get; } = new();

    public ExportPage(string reviewerTitle, List<(string Q, string A)> cards)
    {
        InitializeComponent();
        ReviewerTitle = reviewerTitle;
        foreach (var c in cards)
            Cards.Add(new CardPreview { Question = c.Q, Answer = c.A });
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this);
    }

    // Legacy/demo ctor kept (not used in new flow)
    public ExportPage(string reviewerTitle = "Math Reviewer", int questions = 50)
    {
        InitializeComponent();
        ReviewerTitle = reviewerTitle;
        BindingContext = this;
        PageHelpers.SetupHamburgerMenu(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await mindvault.Utils.AnimHelpers.SlideFadeInAsync(Content);
    }

    private async void OnExportTapped(object? sender, EventArgs e)
    {
        try
        {
            if (Cards is null || Cards.Count == 0)
            {
                await PageHelpers.SafeDisplayAlertAsync(this, "Export", "No cards to export.", "OK");
                return;
            }

            var lines = new List<string> { $"Reviewer: {ReviewerTitle}", $"Questions: {Cards.Count}", string.Empty };
            
            // Export progress data if available
            var progressData = ExportProgressData();
            if (!string.IsNullOrEmpty(progressData))
            {
                lines.Add("Progress: ENABLED");
                lines.Add($"ProgressData: {progressData}");
                lines.Add(string.Empty);
            }
            
            lines.AddRange(Cards.Select(c => $"Q: {c.Question}\nA: {c.Answer}"));
            var content = string.Join("\n\n", lines);

            var fileName = $"{SanitizeFileName(ReviewerTitle)}.txt";
            await SaveTextToDeviceAsync(fileName, content);

            await PageHelpers.SafeDisplayAlertAsync(this, "Export", $"Exported '{ReviewerTitle}' with progress to device storage.", "OK");
            // Go back to Reviewers page after export
            await NavigationService.ToRoot();
        }
        catch (Exception ex)
        {
            await PageHelpers.SafeDisplayAlertAsync(this, "Export Failed", ex.Message, "OK");
        }
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "reviewer" : safe;
    }

    private async Task SaveTextToDeviceAsync(string fileName, string content)
    {
#if ANDROID
        try
        {
            // Use Android 10+ MediaStore APIs only on API 29+ with recognized OS helper
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var values = new Android.Content.ContentValues();
                values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
                values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "text/plain");
                values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

                var resolver = Android.App.Application.Context.ContentResolver!;
                var uri = resolver.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, values)
                          ?? throw new IOException("Could not create download entry");

                using var outStream = resolver.OpenOutputStream(uri) ?? throw new IOException("Cannot open output stream");
                using var writer = new StreamWriter(outStream);
                await writer.WriteAsync(content);
                await writer.FlushAsync();
                return;
            }
            else
            {
                var fallbackPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                File.WriteAllText(fallbackPath, content);
                return;
            }
        }
        catch
        {
            var fallbackPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            File.WriteAllText(fallbackPath, content);
            return;
        }
#elif WINDOWS
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var winPath = Path.Combine(downloads, fileName);
        File.WriteAllText(winPath, content);
#elif IOS
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var iosPath = Path.Combine(docs, fileName);
        File.WriteAllText(iosPath, content);
#elif MACCATALYST
        var macDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(macDownloads);
        var macPath = Path.Combine(macDownloads, fileName);
        File.WriteAllText(macPath, content);
#else
        var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
        File.WriteAllText(path, content);
#endif
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        Debug.WriteLine($"[ExportPage] Back() -> Previous page");
        await PageHelpers.SafeNavigateAsync(this, async () => await NavigationService.Back(),
            "Could not go back");
    }

    private async void OnCloseTapped(object? sender, EventArgs e)
    {
        Debug.WriteLine($"[ExportPage] Back() -> Previous page");
        await PageHelpers.SafeNavigateAsync(this, async () => await NavigationService.Back(),
            "Could not go back");
    }

    private string ExportProgressData()
    {
        try
        {
            // Try to find the reviewer ID by title to get progress data
            var db = ServiceHelper.GetRequiredService<DatabaseService>();
            var reviewers = db.GetReviewersAsync().GetAwaiter().GetResult();
            var reviewer = reviewers.FirstOrDefault(r => r.Title == ReviewerTitle);
            if (reviewer == null) return string.Empty;
            
            // Get progress data from Preferences
            var progressKey = $"ReviewState_{reviewer.Id}";
            var progressJson = Preferences.Get(progressKey, string.Empty);
            
            if (string.IsNullOrEmpty(progressJson)) return string.Empty;
            
            // Encode as base64 to avoid newline issues in file format
            var bytes = System.Text.Encoding.UTF8.GetBytes(progressJson);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExportPage] Failed to export progress: {ex.Message}");
            return string.Empty;
        }
    }
}
