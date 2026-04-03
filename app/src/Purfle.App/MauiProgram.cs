using Microsoft.Extensions.Logging;
using Purfle.App.Pages;
using Purfle.App.Services;
using Purfle.App.ViewModels;
using Purfle.Runtime.Identity;

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

        // Notification service — platform-specific
#if WINDOWS
        builder.Services.AddSingleton<INotificationService, WindowsNotificationService>();
#elif MACCATALYST
        builder.Services.AddSingleton<INotificationService, MacNotificationService>();
#else
        builder.Services.AddSingleton<INotificationService, NullNotificationService>();
#endif

        // Services
        builder.Services.AddSingleton<MarketplaceService>();
        builder.Services.AddSingleton<AgentStore>();
        builder.Services.AddSingleton<IKeyRegistry>(sp =>
            new HttpKeyRegistryClient(
                "https://purfle-key-registry-bxa8bmejh6hhdfe0.centralus-01.azurewebsites.net"));
        builder.Services.AddSingleton<CredentialService>();
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
                try
                {
                    var promptsDir = Path.Combine(Path.GetDirectoryName(file)!, "prompts");
                    var prompts = Directory.Exists(promptsDir) ? promptsDir : null;
                    scheduler.Register(loader.Load(file), prompts);
                }
                catch (Exception ex)
                      { System.Diagnostics.Debug.WriteLine($"[Purfle] Skipping {file}: {ex.Message}"); }
            }

            // Also scan subdirectories for agent.manifest.json (installed agent bundles)
            foreach (var dir in Directory.EnumerateDirectories(agentsDir))
            {
                var manifestPath = Path.Combine(dir, "agent.manifest.json");
                if (!File.Exists(manifestPath)) continue;
                try
                {
                    var promptsDir = Path.Combine(dir, "prompts");
                    var prompts = Directory.Exists(promptsDir) ? promptsDir : null;
                    scheduler.Register(loader.Load(manifestPath), prompts);
                }
                catch (Exception ex)
                      { System.Diagnostics.Debug.WriteLine($"[Purfle] Skipping {dir}: {ex.Message}"); }
            }

            _ = scheduler.StartAsync();
            return scheduler;
        });

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();

        // Pages
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<AgentDetailPage>();
        builder.Services.AddTransient<MyAgentsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AgentRunPage>();
        builder.Services.AddTransient<LogViewPage>();
        builder.Services.AddTransient<SetupWizardPage>();
        builder.Services.AddTransient<ConsentPage>();
        builder.Services.AddTransient<RunHistoryPage>();
        builder.Services.AddTransient<RunDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
