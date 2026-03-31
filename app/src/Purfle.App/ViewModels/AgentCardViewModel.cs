namespace Purfle.App.ViewModels;

using Purfle.Runtime.Scheduling;
using System.ComponentModel;
using System.Windows.Input;

public sealed class AgentCardViewModel : INotifyPropertyChanged
{
    private readonly AgentRunner _runner;
    private IDispatcherTimer?    _timer;

    public string Name       => _runner.Manifest.Name;
    public string OutputPath => _runner.OutputPath;

    public AgentStatus Status      { get; private set; }
    public Color       StatusColor => Status switch
    {
        AgentStatus.Running => Colors.Green,
        AgentStatus.Error   => Colors.Red,
        _                   => Colors.Gray,
    };
    public string StatusText  => Status.ToString();
    public string LastRunText => _runner.LastRun?.ToString("g") ?? "Never";
    public string NextRunText => _runner.NextRun?.ToString("g") ?? "—";

    public ICommand ViewLogCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentCardViewModel(AgentRunner runner)
    {
        _runner = runner;
        Status  = runner.Status;
        ViewLogCommand = new Command(async () =>
            await Shell.Current.GoToAsync(
                $"LogViewPage?outputPath={Uri.EscapeDataString(OutputPath)}"));
    }

    public void StartPolling(IDispatcher dispatcher)
    {
        _timer          = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick    += (_, _) => Refresh();
        _timer.Start();
    }

    public void StopPolling() => _timer?.Stop();

    private void Refresh()
    {
        Status = _runner.Status;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastRunText));
        OnPropertyChanged(nameof(NextRunText));
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
