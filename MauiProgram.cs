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

        // Database service
        builder.Services.AddSingleton(sp =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            var db = new DatabaseService(dbPath);
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
