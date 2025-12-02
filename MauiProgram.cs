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
        // Initialize SQLCipher before any database operations
        // The bundle_e_sqlcipher package automatically provides the encrypted SQLite provider
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

        // Encryption key service (must be registered first)
        builder.Services.AddSingleton<EncryptionKeyService>();

        // Database migration service
        builder.Services.AddSingleton<DatabaseMigrationService>();

        // Database service with encryption
        builder.Services.AddSingleton(sp =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            
            // SIMPLE APPROACH: Delete corrupted database and start fresh
            // This is the fastest way to recover from encryption key mismatches
            if (File.Exists(dbPath))
            {
                try
                {
                    // Try to check if file is valid SQLite database
                    var testBytes = File.ReadAllBytes(dbPath);
                    if (testBytes.Length < 16 || 
                        System.Text.Encoding.ASCII.GetString(testBytes, 0, 16) != "SQLite format 3\0")
                    {
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] Database file is corrupted or encrypted. Deleting...");
                        File.Delete(dbPath);
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] Corrupted database deleted successfully");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MauiProgram] Could not validate database file: {ex.Message}");
                    // Continue anyway - will try to open it
                }
            }
            
            // For now, let's just use UNENCRYPTED database to get the app working
            // You can re-enable encryption later once everything is stable
            System.Diagnostics.Debug.WriteLine("[MauiProgram] Initializing database WITHOUT encryption for stability");
            
            try
            {
                var db = new DatabaseService(dbPath, null);  // null = no encryption
                Task.Run(() => db.InitializeAsync()).Wait();
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Database initialized successfully (unencrypted)");
                return db;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] CRITICAL: Cannot create database: {ex.Message}");
                
                // Last resort: delete and try one more time
                try
                {
                    if (File.Exists(dbPath))
                    {
                        // Wait a moment for any file locks to release
                        System.Threading.Thread.Sleep(500);
                        File.Delete(dbPath);
                        System.Diagnostics.Debug.WriteLine("[MauiProgram] Deleted problematic database file");
                    }
                    
                    var db = new DatabaseService(dbPath, null);
                    Task.Run(() => db.InitializeAsync()).Wait();
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] Fresh database created successfully");
                    return db;
                }
                catch (Exception finalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MauiProgram] FATAL: Cannot initialize database: {finalEx.Message}");
                    throw new InvalidOperationException("Cannot start app - database initialization failed. Please restart the app.", finalEx);
                }
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

#if DEBUG
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();

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
