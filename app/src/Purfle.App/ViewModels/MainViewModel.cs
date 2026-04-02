namespace Purfle.App.ViewModels;

using Purfle.Runtime.Scheduling;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Scheduler _scheduler;
    private bool _isRefreshing;

    public ObservableCollection<AgentCardViewModel> Agents { get; } = new();
    public ICommand AddAgentCommand { get; }
    public ICommand RefreshCommand  { get; }
    public ICommand SortCommand     { get; }

    public bool HasAgents => Agents.Count > 0;
    public bool IsEmpty   => Agents.Count == 0;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set { _isRefreshing = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(Scheduler scheduler)
    {
        _scheduler = scheduler;
        foreach (var runner in scheduler.Runners)
            Agents.Add(new AgentCardViewModel(runner));

        Agents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAgents));
            OnPropertyChanged(nameof(IsEmpty));
        };

        AddAgentCommand = new Command(async () => await AddAgentAsync());
        RefreshCommand  = new Command(() =>
        {
            // Re-sync from scheduler
            var existing = Agents.Select(a => a.Name).ToHashSet();
            foreach (var runner in _scheduler.Runners)
            {
                if (!existing.Contains(runner.Manifest.Name))
                    Agents.Add(new AgentCardViewModel(runner));
            }
            IsRefreshing = false;
        });
        SortCommand = new Command<string>(SortBy);
    }

    private void SortBy(string criterion)
    {
        var sorted = criterion switch
        {
            "name"    => Agents.OrderBy(a => a.Name).ToList(),
            "lastrun" => Agents.OrderByDescending(a => a.LastRunText).ToList(),
            "nextrun" => Agents.OrderBy(a => a.NextRunText).ToList(),
            "status"  => Agents.OrderByDescending(a => a.Status).ToList(),
            _         => Agents.ToList(),
        };

        Agents.Clear();
        foreach (var agent in sorted)
            Agents.Add(agent);
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

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
