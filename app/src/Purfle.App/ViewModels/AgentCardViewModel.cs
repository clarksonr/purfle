namespace Purfle.App.ViewModels;

using Purfle.Runtime.Scheduling;
using System.ComponentModel;
using System.Windows.Input;

public sealed class AgentCardViewModel : INotifyPropertyChanged
{
    private readonly AgentRunner _runner;
    private IDispatcherTimer?    _timer;
    private bool                 _errorExpanded;
    private double               _statusDotOpacity = 1.0;

    public string Name       => _runner.Manifest.Name;
    public string OutputPath => _runner.OutputPath;

    public AgentStatus Status      { get; private set; }
    public Color       StatusColor => Status switch
    {
        AgentStatus.Running => Colors.Orange,
        AgentStatus.Error   => Colors.Red,
        AgentStatus.Idle    => _runner.LastRun.HasValue ? Colors.Green : Colors.Gray,
        _                   => Colors.Gray,
    };
    public double StatusDotOpacity => _statusDotOpacity;
    public string StatusText  => Status switch
    {
        AgentStatus.Running => "Running...",
        AgentStatus.Error   => "Error",
        AgentStatus.Idle    => _runner.LastRun.HasValue ? "Success" : "Idle",
        AgentStatus.Stopped => "Stopped",
        _                   => Status.ToString(),
    };
    public string LastRunText => _runner.LastRun?.ToString("g") ?? "Never";
    public string NextRunText => _runner.NextRun?.ToString("g") ?? "\u2014";

    // Token usage
    public bool HasTokenUsage => _runner.LastTokenUsage.Input > 0 || _runner.LastTokenUsage.Output > 0;
    public string TokenUsageText
    {
        get
        {
            var (input, output) = _runner.LastTokenUsage;
            if (input == 0 && output == 0) return "";
            var duration = _runner.LastRunDuration?.TotalSeconds ?? 0;
            return $"Tokens: {input} in / {output} out | {duration:F1}s";
        }
    }

    // Output preview (last 2 lines of most recent output)
    public bool HasOutputPreview => !string.IsNullOrEmpty(OutputPreview);
    public string OutputPreview { get; private set; } = "";

    // Error badge
    public bool HasError => !string.IsNullOrEmpty(_runner.LastError);
    public string ErrorText => _runner.LastError ?? "";
    public int ErrorMaxLines => _errorExpanded ? 20 : 2;

    public ICommand ViewLogCommand    { get; }
    public ICommand RunNowCommand     { get; }
    public ICommand ToggleErrorCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentCardViewModel(AgentRunner runner)
    {
        _runner = runner;
        Status  = runner.Status;
        ViewLogCommand = new Command(async () =>
            await Shell.Current.GoToAsync(
                $"LogViewPage?outputPath={Uri.EscapeDataString(OutputPath)}"));
        RunNowCommand = new Command(async () =>
        {
            if (_runner.Status == AgentStatus.Running) return;
            await _runner.RunOnceAsync();
            Refresh();
        });
        ToggleErrorCommand = new Command(() =>
        {
            _errorExpanded = !_errorExpanded;
            OnPropertyChanged(nameof(ErrorMaxLines));
        });
        LoadOutputPreview();
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
        var prevStatus = Status;
        Status = _runner.Status;

        // Pulse animation for running status
        _statusDotOpacity = Status == AgentStatus.Running
            ? (DateTime.UtcNow.Second % 2 == 0 ? 1.0 : 0.4)
            : 1.0;

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusDotOpacity));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastRunText));
        OnPropertyChanged(nameof(NextRunText));
        OnPropertyChanged(nameof(HasTokenUsage));
        OnPropertyChanged(nameof(TokenUsageText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorText));

        // Refresh output preview after a run completes
        if (prevStatus == AgentStatus.Running && Status != AgentStatus.Running)
        {
            LoadOutputPreview();
            OnPropertyChanged(nameof(OutputPreview));
            OnPropertyChanged(nameof(HasOutputPreview));
        }
    }

    private void LoadOutputPreview()
    {
        try
        {
            // Look for the most recent output file (not run.log)
            if (!Directory.Exists(OutputPath)) return;
            var files = Directory.GetFiles(OutputPath)
                .Where(f => !f.EndsWith("run.log") && !f.EndsWith("run.jsonl"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (files is null) return;

            var lines = File.ReadLines(files).TakeLast(2).ToArray();
            OutputPreview = string.Join(Environment.NewLine, lines);
        }
        catch
        {
            // Best-effort preview
        }
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
