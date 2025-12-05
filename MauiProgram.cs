using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using mindvault.Services;
using mindvault.Srs;
using Microsoft.Maui.LifecycleEvents;

namespace mindvault;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Initialize SQLCipher for encrypted database support
        SQLitePCL.Batteries_V2.Init();
        
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>()
               .UseMauiCommunityToolkit()
               .ConfigureFonts(fonts =>
               {
                   fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                   fonts.AddFont("fa-solid-900.otf", "FontAwesome6FreeSolid");
                   fonts.AddFont("fa-solid-900.otf", "FAS"); // Keep for backward compatibility
               })
               .ConfigureLifecycleEvents(events =>
               {
#if ANDROID
                   events.AddAndroid(android =>
                   {
                       android.OnPause(_ => TrySaveEngine());
                       android.OnStop(_ => TrySaveEngine());
                   });
#endif
#if IOS
                   events.AddiOS(ios =>
                   {
                       ios.OnResignActivation(_ => TrySaveEngine());
                       ios.WillTerminate(_ => TrySaveEngine());
                   });
#endif
#if WINDOWS
                   events.AddWindows(win =>
                   {
                       win.OnWindowCreated(window =>
                       {
                           window.Closed += (_, _) => TrySaveEngine();
                       });
                   });
#endif
               });

        // Database migration service (for future use if needed)
        builder.Services.AddSingleton<DatabaseMigrationService>();

        // Database service WITH encryption (for capstone security requirements)
        builder.Services.AddSingleton(sp =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            
            System.Diagnostics.Debug.WriteLine("[MauiProgram] Initializing encrypted database");
            System.Diagnostics.Debug.WriteLine($"[MauiProgram] Database path: {dbPath}");
            
            // CRITICAL FIX: Delete old unencrypted database BEFORE trying to open it
            if (File.Exists(dbPath))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] Found existing database file - checking if it's unencrypted");
                    
                    // Try to read the file header to check if it's an unencrypted SQLite file
                    var fileBytes = File.ReadAllBytes(dbPath);
                    if (fileBytes.Length >= 16)
                    {
                        var header = System.Text.Encoding.ASCII.GetString(fileBytes, 0, 16);
                        if (header.StartsWith("SQLite format 3"))
                        {
                            // This is an UNENCRYPTED database - delete it immediately
                            System.Diagnostics.Debug.WriteLine("[MauiProgram] Detected UNENCRYPTED database - deleting it now");
                            File.Delete(dbPath);
                            System.Diagnostics.Debug.WriteLine("[MauiProgram] Old unencrypted database deleted successfully");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[MauiProgram] Database appears to be encrypted already");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MauiProgram] Could not check database file: {ex.Message}");
                    // If we can't read it, try to delete it anyway
                    try
                    {
                        File.Delete(dbPath);
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] Deleted problematic database file");
                    }
                    catch { }
                }
            }
            
            // Get encryption key from secure storage
            string? encryptionKey = null;
            try
            {
                encryptionKey = Task.Run(() => EncryptionKeyManager.GetOrCreateEncryptionKeyAsync()).Result;
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Encryption key retrieved successfully");
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Key storage: {EncryptionKeyManager.GetStorageLocationInfo()}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] ERROR: Failed to get encryption key: {ex.Message}");
                throw new InvalidOperationException("Cannot initialize database encryption. This is required for security.", ex);
            }
            
            // Try to initialize encrypted database
            try
            {
                var db = new DatabaseService(dbPath, encryptionKey);
                Task.Run(() => db.InitializeAsync()).Wait();
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Encrypted database initialized successfully");
                return db;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] CRITICAL: Cannot create database: {ex.Message}");
                throw new InvalidOperationException($"Cannot start app - encrypted database initialization failed: {ex.Message}", ex);
            }
        });

        // Global in-memory flashcard cache
        builder.Services.AddSingleton<FlashcardMemoryCacheService>();
        builder.Services.AddSingleton<DeckMemoryCacheService>();
        builder.Services.AddSingleton<GlobalDeckPreloadService>();

        builder.Services.AddSingleton<ReviewersCacheService>();
        builder.Services.AddSingleton<SrsEngine>();
        builder.Services.AddSingleton<PythonBootstrapper>();
        builder.Services.AddSingleton<FileTextExtractor>();
        builder.Services.AddSingleton<PythonFlashcardService>();
        builder.Services.AddSingleton<MultiplayerService>();
        builder.Services.AddSingleton<AppDataResetService>(); // Data reset utility

#if DEBUG
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();

        // NOTE: Python bootstrap is intentionally NOT done at startup.
        // It should only happen when user navigates to SummarizeContentPage and clicks "AI Summarize".
        // Running it at startup causes race conditions if user navigates to AI features before extraction completes.

        // Kick off reviewer metadata preload (safe, does not touch App services prematurely)
        _ = Task.Run(async () =>
        {
            try
            {
                var svc = app.Services.GetRequiredService<ReviewersCacheService>();
                await svc.PreloadAsync();
            }
            catch { }
        });

        // Kick off flashcard memory preload
        _ = Task.Run(async () =>
        {
            try
            {
                var mem = app.Services.GetRequiredService<FlashcardMemoryCacheService>();
                await mem.PreloadAllAsync();
            }
            catch { }
        });

        // Kick off global deck preload
        _ = Task.Run(async () =>
        {
            try
            {
                var preload = app.Services.GetRequiredService<GlobalDeckPreloadService>();
                await preload.PreloadAllAsync();
            }
            catch { }
        });

        return app;
    }

    private static void TrySaveEngine()
    {
        try
        {
            var engine = ServiceHelper.GetRequiredService<SrsEngine>();
            engine.SaveProgress();
        }
        catch { }
    }
}
