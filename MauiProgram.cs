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
            
            // Get or create encryption key
            var keyService = sp.GetRequiredService<EncryptionKeyService>();
            string encryptionKey;
            try
            {
                encryptionKey = keyService.GetOrCreateKeyAsync().GetAwaiter().GetResult();
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Database encryption key retrieved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Failed to get encryption key: {ex.Message}");
                throw new InvalidOperationException("Failed to initialize database encryption", ex);
            }

            // Check if migration is needed (unencrypted -> encrypted)
            var migrationService = sp.GetRequiredService<DatabaseMigrationService>();
            bool needsMigration = migrationService.NeedsMigrationAsync().GetAwaiter().GetResult();
            
            if (needsMigration)
            {
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Unencrypted database detected, starting automatic migration...");
                var (success, message) = migrationService.MigrateToEncryptedAsync(encryptionKey).GetAwaiter().GetResult();
                
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine($"[MauiProgram] Migration successful: {message}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MauiProgram] Migration failed: {message}");
                    throw new InvalidOperationException($"Database migration failed: {message}");
                }
            }
            
            var db = new DatabaseService(dbPath, encryptionKey);
            Task.Run(() => db.InitializeAsync()).Wait();
            return db;
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
