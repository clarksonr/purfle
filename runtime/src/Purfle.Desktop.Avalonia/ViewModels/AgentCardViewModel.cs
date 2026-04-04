using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Purfle.Runtime.Scheduling;

namespace Purfle.Desktop.Avalonia.ViewModels;

public sealed class AgentCardViewModel : INotifyPropertyChanged
{
    private readonly AgentRunner _runner;
    private DispatcherTimer? _timer;

    public string Name       => _runner.Manifest.Name;
    public string OutputPath => _runner.OutputPath;

    public AgentStatus Status { get; private set; }

    public string StatusText => Status switch
    {
        AgentStatus.Running => "Running...",
        AgentStatus.Error   => "Error",
        AgentStatus.Idle    => _runner.LastRun.HasValue ? "Success" : "Idle",
        AgentStatus.Stopped => "Stopped",
        _                   => Status.ToString(),
    };

    public string StatusColor => Status switch
    {
        AgentStatus.Running => "#FFA500",
        AgentStatus.Error   => "#FF0000",
        AgentStatus.Idle    => _runner.LastRun.HasValue ? "#00CC00" : "#888888",
        _                   => "#888888",
    };

    public string LastRunText => FormatRelativeTime(_runner.LastRun);
    public string NextRunText => _runner.NextRun?.ToString("g") ?? "\u2014";

    public bool HasError   => !string.IsNullOrEmpty(_runner.LastError);
    public string ErrorText => _runner.LastError ?? "";

    public string OutputPreview { get; private set; } = "";
    public bool HasOutputPreview => !string.IsNullOrEmpty(OutputPreview);

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentCardViewModel(AgentRunner runner)
    {
        _runner = runner;
        Status  = runner.Status;
        LoadOutputPreview();
        StartPolling();
    }

    public async void RunNow()
    {
        if (_runner.Status == AgentStatus.Running) return;
        await _runner.RunOnceAsync();
        Refresh();
    }

    private void StartPolling()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        var prev = Status;
        Status = _runner.Status;

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(LastRunText));
        OnPropertyChanged(nameof(NextRunText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorText));

        if (prev == AgentStatus.Running && Status != AgentStatus.Running)
        {
            LoadOutputPreview();
            OnPropertyChanged(nameof(OutputPreview));
            OnPropertyChanged(nameof(HasOutputPreview));

            if (Status == AgentStatus.Error)
                SendNotification($"{Name} failed", _runner.LastError ?? "Unknown error");
            else if (Status == AgentStatus.Idle)
                SendNotification($"{Name} completed", "Agent run finished successfully.");
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
            if (text.Length > 100) text = text[..100] + "...";
            OutputPreview = text.ReplaceLineEndings(" ");
        }
        catch { /* best-effort */ }
    }

    private static void SendNotification(string title, string body)
    {
        // Linux: try libnotify via notify-send
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("notify-send", $"\"{title}\" \"{body}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Fallback: console
            Console.WriteLine($"[Notification] {title}: {body}");
        }
    }

    internal static string FormatRelativeTime(DateTime? utcTime)
    {
        if (utcTime is null) return "Never";
        var elapsed = DateTime.UtcNow - utcTime.Value;
        if (elapsed.TotalSeconds < 60) return "Just now";
        if (elapsed.TotalMinutes < 2)  return "1 minute ago";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes} minutes ago";
        if (elapsed.TotalHours < 2)    return "1 hour ago";
        if (elapsed.TotalHours < 24)   return $"{(int)elapsed.TotalHours} hours ago";
        return utcTime.Value.ToLocalTime().ToString("MMM d, h:mm tt");
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
