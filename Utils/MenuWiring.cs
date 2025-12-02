using CommunityToolkit.Maui.Views;
using mindvault.Controls;
using mindvault.Controls;
using mindvault.Pages;
using mindvault.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace mindvault.Utils;

public static class MenuWiring
{
    public static void Wire(BottomSheetMenu menu, INavigation nav)
    {
        // Header tap -> Home (absolute to root)
        menu.HeaderTapped += async (_, __) =>
        {
            if (Shell.Current is not null)
                await Navigator.GoToAsync($"///{nameof(HomePage)}");
            else
                await Navigator.PopToRootAsync(nav);
        };

        // Create Reviewer (absolute navigation to reset stack)
        menu.CreateTapped += async (_, __) =>
        {
            if (Shell.Current is not null)
                await Navigator.GoToAsync($"///{nameof(TitleReviewerPage)}");
            else
                await Navigator.PushAsync(new TitleReviewerPage(), nav);
        };

        // Browse Reviewer (absolute navigation to reset stack)
        menu.BrowseTapped += async (_, __) =>
        {
            if (Shell.Current is not null)
                await Navigator.GoToAsync($"///{nameof(ReviewersPage)}");
            else
                await Navigator.PushAsync(new ReviewersPage(), nav);
        };

        // Multiplayer Mode (registered route, keep normal push)
        menu.MultiplayerTapped += async (_, __) =>
        {
            if (Shell.Current is not null)
                await Navigator.GoToAsync(nameof(MultiplayerPage));
            else
                await Navigator.PushAsync(new MultiplayerPage(), nav);
        };

        // Import -> JSON only
        menu.ImportTapped += async (_, __) =>
        {
            try
            {
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
                if (pick is null) return;

                // Extension check for JSON only
                var extension = Path.GetExtension(pick.FileName)?.ToLowerInvariant();
                if (extension != ".json")
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.ShowPopupAsync(new AppModal("Import", "Only JSON files are supported.", "OK"));
                    return;
                }

                string content;
                using (var stream = await pick.OpenReadAsync())
                using (var reader = new StreamReader(stream))
                    content = await reader.ReadToEndAsync();

                // Check if encrypted
                if (mindvault.Services.ExportEncryptionService.IsEncrypted(content))
                {
                    // Ask for password using custom modal
                    var passwordModal = new PasswordInputModal(
                        "Password Required",
                        "This file is password-protected. Enter the password:",
                        "Password");
                    
                    var passwordResult = Application.Current?.MainPage != null 
                        ? await Application.Current.MainPage.ShowPopupAsync(passwordModal)
                        : null;
                    var password = passwordResult as string;
                    
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        try
                        {
                            content = mindvault.Services.ExportEncryptionService.Decrypt(content, password);
                        }
                        catch (CryptographicException)
                        {
                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.ShowPopupAsync(new AppModal("Import Failed", "Incorrect password. The file could not be decrypted.", "OK"));
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (Application.Current?.MainPage != null)
                                await Application.Current.MainPage.ShowPopupAsync(new AppModal("Import Failed", $"Decryption error: {ex.Message}", "OK"));
                            return;
                        }
                    }
                    else
                    {
                        // User cancelled password entry
                        return;
                    }
                }

                // Parse JSON format
                var (title, cards, progressData) = ParseJsonExport(content);

                if (cards.Count == 0)
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.ShowPopupAsync(new AppModal("Import", "No cards found in file.", "OK"));
                    return;
                }

                var importPage = new ImportPage(title, cards);
                if (!string.IsNullOrEmpty(progressData))
                {
                    importPage.SetProgressData(progressData);
                }
                await Navigator.PushAsync(importPage, nav);
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                    await Application.Current.MainPage.ShowPopupAsync(new AppModal("Import Failed", ex.Message, "OK"));
            }
        };

        // Settings -> open ProfileSettingsPage
        menu.SettingsTapped += async (_, __) =>
        {
            if (Shell.Current is not null)
                await Navigator.GoToAsync(nameof(ProfileSettingsPage));
            else
                await Navigator.PushAsync(new ProfileSettingsPage(), nav);
        };
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
                .Select(c => (c.Question ?? string.Empty, c.Answer ?? string.Empty))
                .ToList() ?? new List<(string, string)>();

            var progressData = export.Progress?.Enabled == true && !string.IsNullOrEmpty(export.Progress?.Data)
                ? export.Progress.Data
                : string.Empty;

            return (export.Title ?? "Imported Reviewer", cards, progressData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MenuWiring] JSON parse error: {ex.Message}");
            throw new InvalidDataException("Invalid JSON export file format.");
        }
    }
}
