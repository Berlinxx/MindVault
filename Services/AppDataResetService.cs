using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace mindvault.Services;

/// <summary>
/// Service to reset all app data (similar to "Clear Data" on Android).
/// This deletes:
/// - SQLite database (all flashcards and reviewers)
/// - Preferences (all settings)
/// - LocalApplicationData (Python, models, runtime files)
/// </summary>
public class AppDataResetService
{
    /// <summary>
    /// Resets ALL app data. This is equivalent to uninstalling and reinstalling the app.
    /// WARNING: This will delete all user data and cannot be undone!
    /// </summary>
    public async Task<(bool Success, string Message)> ResetAllDataAsync()
    {
        try
        {
            Debug.WriteLine("[AppDataReset] Starting complete app data reset...");
            
            // 1. Clear Preferences
            Debug.WriteLine("[AppDataReset] Clearing preferences...");
            Preferences.Clear();
            
            // 2. Delete Database
            Debug.WriteLine("[AppDataReset] Deleting database...");
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            var backupPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault_backup_unencrypted.db3");
            
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); Debug.WriteLine($"[AppDataReset] Deleted: {dbPath}"); }
                catch (Exception ex) { Debug.WriteLine($"[AppDataReset] Failed to delete database: {ex.Message}"); }
            }
            
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); Debug.WriteLine($"[AppDataReset] Deleted: {backupPath}"); }
                catch (Exception ex) { Debug.WriteLine($"[AppDataReset] Failed to delete backup: {ex.Message}"); }
            }
            
            // 3. Delete LocalApplicationData (Python, models, etc.)
            Debug.WriteLine("[AppDataReset] Deleting LocalApplicationData...");
            var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindVault");
            
            if (Directory.Exists(localAppData))
            {
                try 
                { 
                    Directory.Delete(localAppData, recursive: true); 
                    Debug.WriteLine($"[AppDataReset] Deleted: {localAppData}"); 
                }
                catch (Exception ex) 
                { 
                    Debug.WriteLine($"[AppDataReset] Failed to delete LocalAppData: {ex.Message}");
                    // Try to delete contents individually
                    await DeleteDirectoryContentsAsync(localAppData);
                }
            }
            
            // 4. Delete AppDataDirectory contents (images, cache, etc.)
            Debug.WriteLine("[AppDataReset] Clearing AppDataDirectory...");
            var appDataDir = FileSystem.AppDataDirectory;
            await DeleteDirectoryContentsAsync(appDataDir, preserveDirectory: true);
            
            // 5. Delete Cache Directory
            Debug.WriteLine("[AppDataReset] Clearing CacheDirectory...");
            var cacheDir = FileSystem.CacheDirectory;
            await DeleteDirectoryContentsAsync(cacheDir, preserveDirectory: true);
            
            Debug.WriteLine("[AppDataReset] Reset complete!");
            return (true, "All app data has been deleted successfully. The app will now close.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataReset] Reset failed: {ex}");
            return (false, $"Reset failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Deletes database only (keeps settings and Python environment).
    /// </summary>
    public async Task<(bool Success, string Message)> ResetDatabaseOnlyAsync()
    {
        try
        {
            Debug.WriteLine("[AppDataReset] Resetting database only...");
            
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            var backupPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault_backup_unencrypted.db3");
            
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
                Debug.WriteLine($"[AppDataReset] Deleted database: {dbPath}");
            }
            
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
                Debug.WriteLine($"[AppDataReset] Deleted backup: {backupPath}");
            }
            
            // Clear flashcard cache
            var preloader = ServiceHelper.GetRequiredService<GlobalDeckPreloadService>();
            preloader.Clear();
            
            Debug.WriteLine("[AppDataReset] Database reset complete!");
            return (true, "Database has been deleted. The app will now restart.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataReset] Database reset failed: {ex}");
            return (false, $"Failed to reset database: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clears Python environment only (keeps database and settings).
    /// </summary>
    public async Task<(bool Success, string Message)> ResetPythonEnvironmentAsync()
    {
        try
        {
            Debug.WriteLine("[AppDataReset] Resetting Python environment...");
            
            var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindVault");
            
            if (Directory.Exists(localAppData))
            {
                Directory.Delete(localAppData, recursive: true);
                Debug.WriteLine($"[AppDataReset] Deleted: {localAppData}");
            }
            
            Debug.WriteLine("[AppDataReset] Python environment reset complete!");
            return (true, "Python environment has been deleted. Click 'AI Summarize' to reinstall.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataReset] Python reset failed: {ex}");
            return (false, $"Failed to reset Python: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clears settings only (keeps database and Python).
    /// </summary>
    public (bool Success, string Message) ResetSettingsOnly()
    {
        try
        {
            Debug.WriteLine("[AppDataReset] Resetting settings...");
            Preferences.Clear();
            Debug.WriteLine("[AppDataReset] Settings reset complete!");
            return (true, "All settings have been reset to defaults.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataReset] Settings reset failed: {ex}");
            return (false, $"Failed to reset settings: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Helper to delete directory contents while optionally preserving the directory itself.
    /// </summary>
    private async Task DeleteDirectoryContentsAsync(string directory, bool preserveDirectory = false)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            
            // Delete all files
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try 
                { 
                    File.Delete(file); 
                    Debug.WriteLine($"[AppDataReset] Deleted file: {file}");
                }
                catch (Exception ex) 
                { 
                    Debug.WriteLine($"[AppDataReset] Failed to delete file {file}: {ex.Message}"); 
                }
            }
            
            // Delete all subdirectories
            foreach (var dir in Directory.GetDirectories(directory))
            {
                try 
                { 
                    Directory.Delete(dir, recursive: true); 
                    Debug.WriteLine($"[AppDataReset] Deleted directory: {dir}");
                }
                catch (Exception ex) 
                { 
                    Debug.WriteLine($"[AppDataReset] Failed to delete directory {dir}: {ex.Message}"); 
                }
            }
            
            // Delete the directory itself if requested
            if (!preserveDirectory)
            {
                try 
                { 
                    Directory.Delete(directory, recursive: true); 
                }
                catch (Exception ex) 
                { 
                    Debug.WriteLine($"[AppDataReset] Failed to delete directory {directory}: {ex.Message}"); 
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataReset] Error deleting directory contents: {ex}");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets information about current app data usage.
    /// </summary>
    public (long DatabaseSize, long LocalAppDataSize, long TotalSize) GetDataUsageInfo()
    {
        long dbSize = 0;
        long localAppDataSize = 0;
        
        try
        {
            // Database size
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            if (File.Exists(dbPath))
            {
                dbSize = new FileInfo(dbPath).Length;
            }
            
            var backupPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault_backup_unencrypted.db3");
            if (File.Exists(backupPath))
            {
                dbSize += new FileInfo(backupPath).Length;
            }
            
            // LocalAppData size
            var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindVault");
            if (Directory.Exists(localAppData))
            {
                localAppDataSize = GetDirectorySize(localAppData);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppDataReset] Failed to get data usage: {ex.Message}");
        }
        
        return (dbSize, localAppDataSize, dbSize + localAppDataSize);
    }
    
    /// <summary>
    /// Gets the total size of a directory and all its contents.
    /// </summary>
    private long GetDirectorySize(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return 0;
            
            long size = 0;
            
            // Add size of all files
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; }
                catch { }
            }
            
            return size;
        }
        catch
        {
            return 0;
        }
    }
}
