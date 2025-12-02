using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using mindvault.Controls;
using mindvault.Pages;
using mindvault.Services;

namespace mindvault.Utils;

/// <summary>
/// Shared import logic used by both hamburger menu and ReviewersPage import button
/// </summary>
public static class ImportHelper
{
    private static bool _isImporting = false;

    public static async Task HandleImportAsync(ContentPage page, INavigation navigation, bool navigateToReviewersPageAfter = false)
    {
        // Prevent multiple simultaneous imports
        if (_isImporting)
        {
            System.Diagnostics.Debug.WriteLine($"[ImportHelper] Import already in progress, ignoring request");
            return;
        }

        _isImporting = true;
        System.Diagnostics.Debug.WriteLine($"[ImportHelper] Import started from {page.GetType().Name}");

        try
        {
            // JSON only
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
                System.Diagnostics.Debug.WriteLine($"[ImportHelper] User cancelled file picker");
                return;
            }

            var extension = Path.GetExtension(pick.FileName)?.ToLowerInvariant();
            if (extension != ".json")
            {
                await page.ShowPopupAsync(new AppModal("Import", "Only JSON files are supported.", "OK"));
                return;
            }

            string content;
            using (var stream = await pick.OpenReadAsync())
            using (var reader = new StreamReader(stream))
                content = await reader.ReadToEndAsync();

            // Check if encrypted
            if (ExportEncryptionService.IsEncrypted(content))
            {
                bool passwordCorrect = false;

                while (!passwordCorrect)
                {
                    // Ask for password using custom modal
                    var passwordModal = new PasswordInputModal(
                        "Password Required",
                        "This file is password-protected. Enter the password:",
                        "Password");

                    var passwordResult = await page.ShowPopupAsync(passwordModal);
                    var password = passwordResult as string;

                    if (string.IsNullOrWhiteSpace(password))
                    {
                        // User cancelled password entry
                        System.Diagnostics.Debug.WriteLine($"[ImportHelper] User cancelled password entry");
                        return;
                    }

                    try
                    {
                        content = ExportEncryptionService.Decrypt(content, password);
                        passwordCorrect = true;
                        System.Diagnostics.Debug.WriteLine($"[ImportHelper] Decryption successful");
                    }
                    catch (CryptographicException)
                    {
                        var retry = await page.ShowPopupAsync(new InfoModal(
                            "Incorrect Password",
                            "The password you entered is incorrect. Would you like to try again?",
                            "Try Again",
                            "Cancel"));

                        var shouldRetry = retry is bool b && b;

                        if (!shouldRetry)
                        {
                            // User chose to cancel
                            System.Diagnostics.Debug.WriteLine($"[ImportHelper] User cancelled after wrong password");
                            return;
                        }
                        // Loop continues to ask for password again
                    }
                    catch (Exception ex)
                    {
                        await page.ShowPopupAsync(new AppModal("Import Failed", $"Decryption error: {ex.Message}", "OK"));
                        return;
                    }
                }
            }

            // Parse JSON
            var (title, cards, progressData) = ParseJsonExport(content);

            if (cards.Count == 0)
            {
                await page.ShowPopupAsync(new AppModal("Import", "No cards found in file.", "OK"));
                return;
            }

            var importPage = new ImportPage(title, cards);
            if (!string.IsNullOrEmpty(progressData))
            {
                importPage.SetProgressData(progressData);
            }

            // Set navigation flag if requested
            if (navigateToReviewersPageAfter)
            {
                importPage.SetNavigateToReviewersPage(true);
            }

            await Navigator.PushAsync(importPage, navigation);
            System.Diagnostics.Debug.WriteLine($"[ImportHelper] Import successful");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImportHelper] Import failed: {ex.Message}");
            await page.ShowPopupAsync(new AppModal("Import Failed", ex.Message, "OK"));
        }
        finally
        {
            _isImporting = false;
            System.Diagnostics.Debug.WriteLine($"[ImportHelper] Import lock released");
        }
    }

    private static (string Title, List<(string Q, string A)> Cards, string ProgressData) ParseJsonExport(string json)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var export = System.Text.Json.JsonSerializer.Deserialize<Models.ReviewerExport>(json, options);
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
            System.Diagnostics.Debug.WriteLine($"[ImportHelper] JSON parse error: {ex.Message}");
            throw new InvalidDataException("Invalid JSON export file format.");
        }
    }
}
