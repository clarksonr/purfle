namespace Purfle.App.ViewModels;

using Purfle.Runtime.Scheduling;
using System.Collections.ObjectModel;
using System.Windows.Input;

public sealed class MainViewModel
{
    private readonly Scheduler _scheduler;

    public ObservableCollection<AgentCardViewModel> Agents { get; } = new();
    public ICommand AddAgentCommand { get; }

    public MainViewModel(Scheduler scheduler)
    {
        _scheduler = scheduler;
        foreach (var runner in scheduler.Runners)
            Agents.Add(new AgentCardViewModel(runner));

        AddAgentCommand = new Command(async () => await AddAgentAsync());
    }

    private async Task AddAgentAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Agent Manifest (.json)",
            FileTypes   = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".json"],
                [DevicePlatform.macOS] = ["json"],
            }),
        });

        if (result is null) return;

        try
        {
            var agentsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "aivm", "agents");
            Directory.CreateDirectory(agentsDir);

            var destPath = Path.Combine(agentsDir, Path.GetFileName(result.FullPath));
            File.Copy(result.FullPath, destPath, overwrite: true);

            var manifest = new Purfle.Runtime.Manifest.ManifestLoader().Load(destPath);
            _scheduler.Register(manifest);

            var vm = new AgentCardViewModel(_scheduler.Runners[^1]);
            Agents.Add(vm);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
