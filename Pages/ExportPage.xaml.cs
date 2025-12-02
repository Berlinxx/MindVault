using mindvault.Services;
using mindvault.Utils;
using System.Diagnostics;
using mindvault.Data;
using Microsoft.Maui.Storage;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using mindvault.Models;
using System.Text.Json;
using CommunityToolkit.Maui.Views;

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

            Debug.WriteLine($"[ExportPage] Starting export for '{ReviewerTitle}'");

            // Ask user if they want to add password protection
            var addPassword = await this.ShowPopupAsync(
                new Controls.InfoModal(
                    "Password Protection",
                    "Would you like to protect this export with a password? This is recommended for sensitive content.",
                    "Add Password",
                    "No Password"));
            
            bool wantsPassword = addPassword is bool b && b;
            string? password = null;

            if (wantsPassword)
            {
                // Get password from user using custom modal
                var passwordModal = new Controls.PasswordInputModal(
                    "Set Password",
                    "Enter a password to encrypt your export file:",
                    "Password");
                
                var pwdResult = await this.ShowPopupAsync(passwordModal);
                var pwd = pwdResult as string;
                
                if (!string.IsNullOrWhiteSpace(pwd))
                {
                    // Confirm password using custom modal
                    var confirmModal = new Controls.PasswordInputModal(
                        "Confirm Password",
                        "Please enter the same password again:",
                        "Password");
                    
                    var confirmResult = await this.ShowPopupAsync(confirmModal);
                    var confirmPwd = confirmResult as string;
                    
                    if (pwd == confirmPwd)
                    {
                        password = pwd;
                        Debug.WriteLine($"[ExportPage] Password protection enabled");
                    }
                    else
                    {
                        await PageHelpers.SafeDisplayAlertAsync(this, "Export", "Passwords don't match. Export cancelled.", "OK");
                        return;
                    }
                }
                else
                {
                    // User cancelled password entry
                    return;
                }
            }

            // Get progress data asynchronously to avoid blocking
            var progressData = await GetProgressDataAsync();
            Debug.WriteLine($"[ExportPage] Progress data retrieved: {(progressData != null ? "Yes" : "No")}");

            // Build export model
            var export = new ReviewerExport
            {
                Version = 1,
                Title = ReviewerTitle,
                ExportedAt = DateTime.UtcNow,
                CardCount = Cards.Count,
                Cards = Cards.Select(c => new FlashcardExport
                {
                    Question = c.Question,
                    Answer = c.Answer
                }).ToList(),
                Progress = progressData
            };

            // Serialize to JSON with pretty printing
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(export, options);
            Debug.WriteLine($"[ExportPage] JSON serialized, length: {json.Length}");

            // Encrypt if password provided
            if (!string.IsNullOrWhiteSpace(password))
            {
                json = Services.ExportEncryptionService.Encrypt(json, password);
                Debug.WriteLine($"[ExportPage] JSON encrypted, length: {json.Length}");
            }

            var fileName = $"{SanitizeFileName(ReviewerTitle)}.json";
            await SaveTextToDeviceAsync(fileName, json);
            Debug.WriteLine($"[ExportPage] File saved: {fileName}");

            // Only show success dialog if password was used, otherwise navigate immediately
            if (!string.IsNullOrWhiteSpace(password))
            {
                var message = $"Exported '{ReviewerTitle}' with password protection to device storage.";
                Debug.WriteLine($"[ExportPage] Showing success dialog");
                await PageHelpers.SafeDisplayAlertAsync(this, "Export", message, "OK");
                Debug.WriteLine($"[ExportPage] Dialog dismissed");
            }
            else
            {
                // For no password export, just navigate back immediately
                Debug.WriteLine($"[ExportPage] No password - navigating immediately");
            }
            
            // Navigate back
            Debug.WriteLine($"[ExportPage] Navigating to root");
            await NavigationService.ToRoot();
            Debug.WriteLine($"[ExportPage] Navigation complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExportPage] Export error: {ex.Message}");
            Debug.WriteLine($"[ExportPage] Stack trace: {ex.StackTrace}");
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
                values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "application/json");
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
                await File.WriteAllTextAsync(fallbackPath, content);
                return;
            }
        }
        catch
        {
            var fallbackPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(fallbackPath, content);
            return;
        }
#elif WINDOWS
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var winPath = Path.Combine(downloads, fileName);
        await File.WriteAllTextAsync(winPath, content);
#elif IOS
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var iosPath = Path.Combine(docs, fileName);
        await File.WriteAllTextAsync(iosPath, content);
#elif MACCATALYST
        var macDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(macDownloads);
        var macPath = Path.Combine(macDownloads, fileName);
        await File.WriteAllTextAsync(macPath, content);
#else
        var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
        await File.WriteAllTextAsync(path, content);
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

    private async Task<ProgressExport?> GetProgressDataAsync()
    {
        try
        {
            Debug.WriteLine($"[ExportPage] Getting progress data for '{ReviewerTitle}'");
            
            // Try to find the reviewer ID by title to get progress data
            var db = ServiceHelper.GetRequiredService<DatabaseService>();
            var reviewers = await db.GetReviewersAsync();
            Debug.WriteLine($"[ExportPage] Retrieved {reviewers.Count} reviewers from database");
            
            var reviewer = reviewers.FirstOrDefault(r => r.Title == ReviewerTitle);
            if (reviewer == null)
            {
                Debug.WriteLine($"[ExportPage] Reviewer not found: '{ReviewerTitle}'");
                return null;
            }
            
            Debug.WriteLine($"[ExportPage] Found reviewer ID: {reviewer.Id}");
            
            // Get progress data from Preferences
            var progressKey = $"ReviewState_{reviewer.Id}";
            var progressJson = Preferences.Get(progressKey, string.Empty);
            
            if (string.IsNullOrEmpty(progressJson))
            {
                Debug.WriteLine($"[ExportPage] No progress data found for key: {progressKey}");
                return null;
            }
            
            Debug.WriteLine($"[ExportPage] Found progress data, length: {progressJson.Length}");
            
            return new ProgressExport
            {
                Enabled = true,
                Data = progressJson
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExportPage] Failed to export progress: {ex.Message}");
            return null;
        }
    }
}
