namespace Purfle.App.ViewModels;

using Purfle.Runtime.Scheduling;
using System.ComponentModel;
using System.Windows.Input;

public sealed class AgentCardViewModel : INotifyPropertyChanged
{
    private readonly AgentRunner _runner;
    private static readonly Purfle.App.Services.NotificationService s_notifier = new();
    private IDispatcherTimer?    _timer;
    private bool                 _errorExpanded;
    private double               _statusDotOpacity = 1.0;
    private bool                 _pulseHigh = true;

    public AgentRunner Runner => _runner;
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

    public string LastRunText => FormatRelativeTime(_runner.LastRun);
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

    // Output preview (first 100 chars of most recent output)
    public bool HasOutputPreview => !string.IsNullOrEmpty(OutputPreview);
    public string OutputPreview { get; private set; } = "";
    public bool ShowNoOutputYet => !HasOutputPreview;

    // Error badge — first 120 chars, truncated with ellipsis
    public bool HasError => !string.IsNullOrEmpty(_runner.LastError);
    public string ErrorText
    {
        get
        {
            var err = _runner.LastError ?? "";
            if (!_errorExpanded && err.Length > 120)
                return err[..120] + "...";
            return err;
        }
    }
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
            OnPropertyChanged(nameof(ErrorText));
            OnPropertyChanged(nameof(ErrorMaxLines));
        });
        LoadOutputPreview();
    }

    /// <summary>
    /// Starts a 5-second polling timer for status and a smooth pulse animation
    /// for the running state dot.
    /// </summary>
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

        // Pulse animation for running status: toggle between 1.0 and 0.3
        if (Status == AgentStatus.Running)
        {
            _pulseHigh = !_pulseHigh;
            _statusDotOpacity = _pulseHigh ? 1.0 : 0.3;
        }
        else
        {
            _statusDotOpacity = 1.0;
            _pulseHigh = true;
        }

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
        OnPropertyChanged(nameof(ShowNoOutputYet));

        // Refresh output preview after a run completes and fire notifications
        if (prevStatus == AgentStatus.Running && Status != AgentStatus.Running)
        {
            LoadOutputPreview();
            OnPropertyChanged(nameof(OutputPreview));
            OnPropertyChanged(nameof(HasOutputPreview));
            OnPropertyChanged(nameof(ShowNoOutputYet));

            // System tray notifications
            if (Status == AgentStatus.Error)
                s_notifier.NotifyError(Name, _runner.LastError ?? "Unknown error");
            else if (Status == AgentStatus.Idle && _runner.LastRun.HasValue)
                s_notifier.NotifyCompletion(Name);
        }
    }

    private void LoadOutputPreview()
    {
        try
        {
            if (!Directory.Exists(OutputPath)) return;
            var file = Directory.GetFiles(OutputPath)
                .Where(f => !f.EndsWith("run.log") && !f.EndsWith("run.jsonl"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (file is null) return;

            var text = File.ReadAllText(file);
            if (text.Length > 100)
                text = text[..100] + "...";
            OutputPreview = text.ReplaceLineEndings(" ");
        }
        catch
        {
            // Best-effort preview
        }
    }

    /// <summary>
    /// Formats a DateTime as a human-readable relative string.
    /// </summary>
    internal static string FormatRelativeTime(DateTime? utcTime)
    {
        if (utcTime is null) return "Never";

        var now = DateTime.UtcNow;
        var elapsed = now - utcTime.Value;

        if (elapsed.TotalSeconds < 60)
            return "Just now";
        if (elapsed.TotalMinutes < 2)
            return "1 minute ago";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} minutes ago";
        if (elapsed.TotalHours < 2)
            return "1 hour ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours} hours ago";

        // Same calendar day in local time
        var local = utcTime.Value.ToLocalTime();
        if (local.Date == DateTime.Now.Date)
            return $"Today {local:h:mm tt}";
        if (local.Date == DateTime.Now.Date.AddDays(-1))
            return $"Yesterday {local:h:mm tt}";

        return local.ToString("MMM d, h:mm tt");
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
