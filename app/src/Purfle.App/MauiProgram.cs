using Microsoft.Extensions.Logging;
using Purfle.App.Pages;
using Purfle.App.Services;
using Purfle.App.ViewModels;

namespace Purfle.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<MarketplaceService>();
        builder.Services.AddSingleton<AgentStore>();
        builder.Services.AddSingleton<AgentExecutorService>();

        // Scheduler — scans %LOCALAPPDATA%/aivm/agents at startup
        builder.Services.AddSingleton<Purfle.Runtime.Scheduling.Scheduler>(sp =>
        {
            var agentsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "aivm", "agents");
            Directory.CreateDirectory(agentsDir);

            var adapter   = new Purfle.Runtime.Adapters.AnthropicAdapter();
            var scheduler = new Purfle.Runtime.Scheduling.Scheduler(adapter);
            var loader    = new Purfle.Runtime.Manifest.ManifestLoader();

            foreach (var file in Directory.EnumerateFiles(agentsDir, "*.agent.json"))
            {
                try   { scheduler.Register(loader.Load(file)); }
                catch (Exception ex)
                      { System.Diagnostics.Debug.WriteLine($"[Purfle] Skipping {file}: {ex.Message}"); }
            }

            _ = scheduler.StartAsync();
            return scheduler;
        });

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();

        // Pages
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<AgentDetailPage>();
        builder.Services.AddTransient<MyAgentsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AgentRunPage>();
        builder.Services.AddTransient<LogViewPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
