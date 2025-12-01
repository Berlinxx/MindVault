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

        // Import -> only .txt files
        menu.ImportTapped += async (_, __) =>
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

                var pick = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select .txt export file",
                    FileTypes = fileTypes
                });
                if (pick is null) return;

                // Extension enforcement (.txt only)
                if (!string.Equals(Path.GetExtension(pick.FileName), ".txt", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (Application.Current?.MainPage != null)
                        await Application.Current.MainPage.ShowPopupAsync(new AppModal("Import", "Only .txt files are supported.", "OK"));
                    return;
                }

                string content;
                using (var stream = await pick.OpenReadAsync())
                using (var reader = new StreamReader(stream))
                    content = await reader.ReadToEndAsync();

                var (title, cards, progressData) = ParseExport(content);
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

    static (string Title, List<(string Q, string A)> Cards, string ProgressData) ParseExport(string content)
    {
        var lines = content.Replace("\r", string.Empty).Split('\n');
        string title = lines.FirstOrDefault(l => l.StartsWith("Reviewer:", System.StringComparison.OrdinalIgnoreCase))?.Substring(9).Trim() ?? "Imported Reviewer";
        string progressData = string.Empty;
        
        // Check for progress data
        var progressLine = lines.FirstOrDefault(l => l.StartsWith("ProgressData:", System.StringComparison.OrdinalIgnoreCase));
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
            if (line.StartsWith("Reviewer:", System.StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Questions:", System.StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Progress:", System.StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("ProgressData:", System.StringComparison.OrdinalIgnoreCase))
                continue;
                
            if (line.StartsWith("Q:", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(q)) { cards.Add((q, string.Empty)); }
                q = line.Substring(2).Trim();
            }
            else if (line.StartsWith("A:", System.StringComparison.OrdinalIgnoreCase))
            {
                var a = line.Substring(2).Trim();
                if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(a))
                {
                    cards.Add((q ?? string.Empty, a));
                    q = null;
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(q)) cards.Add((q, string.Empty));
        return (title, cards, progressData);
    }
}
