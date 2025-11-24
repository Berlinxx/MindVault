using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using mindvault.Services; // restored for DatabaseService & MultiplayerService
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
                   fonts.AddFont("fa-solid-900.otf", "FAS");
               })
               .ConfigureLifecycleEvents(events =>
               {
#if ANDROID
                   events.AddAndroid(android =>
                   {
                       android.OnPause(activity => TrySaveEngine());
                       android.OnStop(activity => TrySaveEngine());
                   });
#endif
#if IOS
                   events.AddiOS(ios =>
                   {
                       ios.OnResignActivation(app => TrySaveEngine());
                       ios.WillTerminate(app => TrySaveEngine());
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

        // Register SQLite-backed DatabaseService as singleton
        builder.Services.AddSingleton(sp =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mindvault.db3");
            var db = new DatabaseService(dbPath);
            Task.Run(() => db.InitializeAsync()).Wait();
            return db;
        });

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

        // Silent refined preload (does not notify user)
        _ = Task.Run(async () =>
        {
            try
            {
                var cache = ServiceHelper.GetRequiredService<ReviewersCacheService>();
                await cache.PreloadAsync();
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
