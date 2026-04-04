using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Purfle.Desktop.Avalonia.Views;
using Purfle.Desktop.Avalonia.ViewModels;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Scheduling;

namespace Purfle.Desktop.Avalonia;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var scheduler = CreateScheduler();
            var vm = new MainViewModel(scheduler);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };

            _ = scheduler.StartAsync(default);

            desktop.ShutdownRequested += async (_, _) =>
            {
                await scheduler.StopAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Scheduler CreateScheduler()
    {
        var agentsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".purfle", "agents");

        // Use the simplified ILlmAdapter — reads API key from env at request time
        var adapter = new AnthropicAdapter();
        var eventSourceFactory = new SseEventSourceFactory();
        var scheduler = new Scheduler(adapter, eventSourceFactory: eventSourceFactory);

        if (!Directory.Exists(agentsDir))
        {
            Directory.CreateDirectory(agentsDir);
            return scheduler;
        }

        var loader = new Purfle.Runtime.Manifest.ManifestLoader();

        // Scan top-level *.agent.json files
        foreach (var file in Directory.EnumerateFiles(agentsDir, "*.agent.json"))
        {
            try
            {
                var promptsDir = Path.Combine(Path.GetDirectoryName(file)!, "prompts");
                var prompts = Directory.Exists(promptsDir) ? promptsDir : null;
                scheduler.Register(loader.Load(file), prompts);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Purfle] Skipping {file}: {ex.Message}");
            }
        }

        // Scan subdirectories for agent.manifest.json (installed bundles)
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
            {
                Console.Error.WriteLine($"[Purfle] Skipping {dir}: {ex.Message}");
            }
        }

        return scheduler;
    }
}
