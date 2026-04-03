namespace Purfle.App.ViewModels;

using Purfle.Runtime.Scheduling;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly Scheduler _scheduler;
    private IDispatcherTimer? _timer;

    public ObservableCollection<AgentRosterItem> AgentRoster { get; } = new();

    // Summary bar
    public int TotalAgents       { get; private set; }
    public int SuccessToday      { get; private set; }
    public int ErrorCount        { get; private set; }
    public int RunningCount      { get; private set; }

    // Digest
    public string DigestText     { get; private set; } = "";
    public bool HasDigest        { get; private set; }
    public bool NoDigest         => !HasDigest;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DashboardViewModel(Scheduler scheduler)
    {
        _scheduler = scheduler;
        Reload();
    }

    public void StartPolling(IDispatcher dispatcher)
    {
        _timer          = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick    += (_, _) => Reload();
        _timer.Start();
    }

    public void StopPolling() => _timer?.Stop();

    public void Reload()
    {
        var runners = _scheduler.Runners;
        TotalAgents  = runners.Count;
        RunningCount = runners.Count(r => r.Status == AgentStatus.Running);
        ErrorCount   = runners.Count(r => r.Status == AgentStatus.Error);
        SuccessToday = runners.Count(r =>
            r.Status == AgentStatus.Idle && r.LastRun.HasValue &&
            r.LastRun.Value.Date == DateTime.UtcNow.Date);

        // Rebuild roster
        AgentRoster.Clear();
        foreach (var runner in runners)
        {
            AgentRoster.Add(new AgentRosterItem(runner));
        }

        // Load report-builder digest
        LoadDigest(runners);

        OnPropertyChanged(nameof(TotalAgents));
        OnPropertyChanged(nameof(SuccessToday));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(DigestText));
        OnPropertyChanged(nameof(HasDigest));
        OnPropertyChanged(nameof(NoDigest));
    }

    private void LoadDigest(IReadOnlyList<AgentRunner> runners)
    {
        var reportBuilder = runners.FirstOrDefault(r =>
            r.Manifest.Name.Contains("Report Builder", StringComparison.OrdinalIgnoreCase) ||
            r.Manifest.Name.Contains("report-builder", StringComparison.OrdinalIgnoreCase));

        if (reportBuilder is null)
        {
            DigestText = "report-builder hasn't run yet today.";
            HasDigest = false;
            return;
        }

        // Check if report-builder ran today
        if (reportBuilder.LastRun is null || reportBuilder.LastRun.Value.Date != DateTime.UtcNow.Date)
        {
            DigestText = "report-builder hasn't run yet today.";
            HasDigest = false;
            return;
        }

        // Try to read its latest output
        try
        {
            if (!Directory.Exists(reportBuilder.OutputPath))
            {
                DigestText = "report-builder hasn't run yet today.";
                HasDigest = false;
                return;
            }

            var outputFile = Directory.GetFiles(reportBuilder.OutputPath)
                .Where(f => !f.EndsWith("run.log") && !f.EndsWith("run.jsonl"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (outputFile is null)
            {
                DigestText = "report-builder hasn't run yet today.";
                HasDigest = false;
                return;
            }

            DigestText = File.ReadAllText(outputFile);
            HasDigest = true;
        }
        catch
        {
            DigestText = "report-builder hasn't run yet today.";
            HasDigest = false;
        }
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class AgentRosterItem : INotifyPropertyChanged
{
    private readonly AgentRunner _runner;

    public string AgentId   => _runner.Manifest.Id.ToString();
    public string Name      => _runner.Manifest.Name;
    public string LastRun   => AgentCardViewModel.FormatRelativeTime(_runner.LastRun);
    public AgentStatus Status => _runner.Status;
    public Color StatusColor => Status switch
    {
        AgentStatus.Running => Colors.Orange,
        AgentStatus.Error   => Colors.Red,
        AgentStatus.Idle    => _runner.LastRun.HasValue ? Colors.Green : Colors.Gray,
        _                   => Colors.Gray,
    };

    public ICommand ViewDetailCommand { get; }
    public ICommand RunNowCommand     { get; }

    public AgentRosterItem(AgentRunner runner)
    {
        _runner = runner;
        ViewDetailCommand = new Command(async () =>
            await Shell.Current.GoToAsync(
                $"AgentDetailPage?agentId={Uri.EscapeDataString(AgentId)}"));
        RunNowCommand = new Command(async () =>
        {
            if (_runner.Status == AgentStatus.Running) return;
            await _runner.RunOnceAsync();
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
