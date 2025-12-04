using mindvault.Services;
using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using mindvault.Controls;

namespace mindvault.Pages;

public partial class DeveloperToolsPage : ContentPage
{
    private readonly AppDataResetService _resetService;
    private string _databasePath = string.Empty;

    public DeveloperToolsPage()
    {
        InitializeComponent();
        _resetService = ServiceHelper.GetRequiredService<AppDataResetService>();
        LoadDatabaseInfo();
        RefreshUsageInfo();
    }

    private void LoadDatabaseInfo()
    {
        try
        {
            _databasePath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            DatabasePathLabel.Text = _databasePath;
            
            Debug.WriteLine($"[DeveloperTools] Database path: {_databasePath}");
            Debug.WriteLine($"[DeveloperTools] Database exists: {File.Exists(_databasePath)}");
        }
        catch (Exception ex)
        {
            DatabasePathLabel.Text = $"Error: {ex.Message}";
            Debug.WriteLine($"[DeveloperTools] Error loading database info: {ex}");
        }
    }

    private void RefreshUsageInfo()
    {
        try
        {
            var (dbSize, localAppDataSize, totalSize) = _resetService.GetDataUsageInfo();
            
            DatabaseSizeLabel.Text = FormatBytes(dbSize);
            LocalAppDataSizeLabel.Text = FormatBytes(localAppDataSize);
            TotalSizeLabel.Text = FormatBytes(totalSize);
            
            Debug.WriteLine($"[DeveloperTools] Database size: {FormatBytes(dbSize)}");
            Debug.WriteLine($"[DeveloperTools] LocalAppData size: {FormatBytes(localAppDataSize)}");
            Debug.WriteLine($"[DeveloperTools] Total size: {FormatBytes(totalSize)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeveloperTools] Error refreshing usage info: {ex}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private async void OnCopyDatabasePathClicked(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.SetTextAsync(_databasePath);
            ShowStatus("Path copied to clipboard!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeveloperTools] Error copying path: {ex}");
            await DisplayAlert("Error", $"Failed to copy path: {ex.Message}", "OK");
        }
    }

    private async void OnOpenDatabaseFolderClicked(object? sender, EventArgs e)
    {
        try
        {
#if WINDOWS
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Process.Start("explorer.exe", directory);
                ShowStatus("Folder opened in Explorer");
            }
#else
            await Clipboard.SetTextAsync(Path.GetDirectoryName(_databasePath) ?? _databasePath);
            await DisplayAlert("Folder Path", 
                $"Path copied to clipboard:\n{Path.GetDirectoryName(_databasePath)}", 
                "OK");
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeveloperTools] Error opening folder: {ex}");
            await DisplayAlert("Error", $"Failed to open folder: {ex.Message}", "OK");
        }
    }

    private void OnRefreshUsageClicked(object? sender, EventArgs e)
    {
        RefreshUsageInfo();
        ShowStatus("Usage info refreshed");
    }

    private async void OnResetDatabaseClicked(object? sender, EventArgs e)
    {
        var confirm = await this.ShowPopupAsync(
            new AppModal(
                "Reset Database",
                "This will delete all flashcards and reviewers. Your settings and Python environment will be preserved.\n\nThis action cannot be undone!",
                "Reset Database",
                "Cancel"));

        if (confirm is bool b && b)
        {
            Debug.WriteLine("[DeveloperTools] Resetting database...");
            var (success, message) = await _resetService.ResetDatabaseOnlyAsync();
            
            if (success)
            {
                await DisplayAlert("Success", message, "OK");
                await Shell.Current.GoToAsync("///ReviewersPage");
            }
            else
            {
                await DisplayAlert("Error", message, "OK");
            }
        }
    }

    private async void OnResetPythonClicked(object? sender, EventArgs e)
    {
        var confirm = await this.ShowPopupAsync(
            new AppModal(
                "Reset Python Environment",
                "This will delete the Python installation and AI models. Your database and settings will be preserved.\n\nYou'll need to reinstall Python by clicking 'AI Summarize' again.",
                "Reset Python",
                "Cancel"));

        if (confirm is bool b && b)
        {
            Debug.WriteLine("[DeveloperTools] Resetting Python environment...");
            var (success, message) = await _resetService.ResetPythonEnvironmentAsync();
            
            await DisplayAlert(success ? "Success" : "Error", message, "OK");
            RefreshUsageInfo();
        }
    }

    private async void OnResetSettingsClicked(object? sender, EventArgs e)
    {
        var confirm = await this.ShowPopupAsync(
            new AppModal(
                "Reset Settings",
                "This will reset all app settings to defaults. Your database and Python environment will be preserved.",
                "Reset Settings",
                "Cancel"));

        if (confirm is bool b && b)
        {
            Debug.WriteLine("[DeveloperTools] Resetting settings...");
            var (success, message) = _resetService.ResetSettingsOnly();
            
            await DisplayAlert(success ? "Success" : "Error", message, "OK");
        }
    }

    private async void OnResetAllDataClicked(object? sender, EventArgs e)
    {
        var confirm = await this.ShowPopupAsync(
            new AppModal(
                "?? RESET ALL DATA",
                "This will delete EVERYTHING:\n\n• All flashcards and reviewers\n• All settings\n• Python and AI models\n• All cached data\n\nYou will see the onboarding screens again.\n\nThis action CANNOT be undone!",
                "DELETE EVERYTHING",
                "Cancel"));

        if (confirm is bool b && b)
        {
            // Double confirm
            var doubleConfirm = await DisplayAlert(
                "Final Confirmation",
                "Are you absolutely sure you want to delete ALL data?",
                "Yes, Delete Everything",
                "No, Cancel");

            if (doubleConfirm)
            {
                Debug.WriteLine("[DeveloperTools] Resetting ALL data...");
                var (success, message) = await _resetService.ResetAllDataAsync();
                
                if (success)
                {
                    await DisplayAlert("Success", 
                        "All data has been deleted. The app will now restart and show the onboarding screens.", 
                        "OK");
                    
                    // Navigate to onboarding
                    await Shell.Current.GoToAsync("///OnboardingPage");
                }
                else
                {
                    await DisplayAlert("Error", message, "OK");
                }
            }
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void ShowStatus(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
        
        // Hide after 3 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            MainThread.BeginInvokeOnMainThread(() => StatusLabel.IsVisible = false);
        });
    }
}
