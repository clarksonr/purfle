using NCrontab;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Scheduling;

/// <summary>
/// Manages a pool of <see cref="AgentRunner"/> instances and drives each one
/// according to the trigger declared in its manifest's <c>schedule</c> block.
///
/// <list type="bullet">
///   <item><term>startup</term><description>runs once immediately on <see cref="StartAsync"/>.</description></item>
///   <item><term>interval</term><description>fires on a <see cref="PeriodicTimer"/> loop.</description></item>
///   <item><term>cron</term><description>uses <see cref="CrontabSchedule"/> (NCrontab) to sleep until the next occurrence.</description></item>
///   <item><term>window</term><description>fires relative to a declared time window (open, close, or interval within).</description></item>
///   <item><term>event</term><description>fires when an MCP server emits a named event.</description></item>
/// </list>
///
/// <para>
/// All runners execute concurrently on their own background tasks. The scheduler
/// catches and logs per-loop errors so that a single bad runner can never crash
/// the host.
/// </para>
/// </summary>
public sealed class Scheduler
{
    private readonly ILlmAdapter                        _llmAdapter;
    private readonly Func<AgentManifest, TimeSpan>?     _intervalFactory;
    private readonly List<AgentRunner>                  _runners = new();
    private readonly List<Task>                         _loops   = new();
    private CancellationTokenSource?                    _cts;
    private readonly IEventSourceFactory?               _eventSourceFactory;

    public IReadOnlyList<AgentRunner> Runners => _runners.AsReadOnly();

    /// <param name="llmAdapter">Adapter shared by all registered runners.</param>
    /// <param name="intervalFactory">
    /// Optional override for the interval computation. Defaults to
    /// <c>TimeSpan.FromMinutes(schedule.interval_minutes)</c>. Inject a shorter
    /// factory in tests to avoid wall-clock waits.
    /// </param>
    /// <param name="eventSourceFactory">Optional factory for creating event source connections.</param>
    public Scheduler(ILlmAdapter llmAdapter,
                     Func<AgentManifest, TimeSpan>? intervalFactory = null,
                     IEventSourceFactory? eventSourceFactory = null)
    {
        _llmAdapter          = llmAdapter;
        _intervalFactory     = intervalFactory;
        _eventSourceFactory  = eventSourceFactory;
    }

    /// <summary>
    /// Creates an <see cref="AgentRunner"/> for <paramref name="manifest"/>,
    /// computes its initial <c>NextRun</c>, and adds it to <see cref="Runners"/>.
    /// </summary>
    public void Register(AgentManifest manifest, string? promptsDirectory = null)
    {
        var runner = new AgentRunner(manifest, _llmAdapter, promptsDirectory);

        if (manifest.Schedule is { } sched)
        {
            runner.NextRun = sched.Trigger switch
            {
                "startup"  => DateTime.UtcNow,
                "interval" => DateTime.UtcNow.Add(GetInterval(manifest)),
                "cron"     => sched.Cron is { } cron
                              ? CrontabSchedule.Parse(cron).GetNextOccurrence(DateTime.UtcNow)
                              : null,
                "window"   => ComputeNextWindowRun(sched),
                "event"    => null,
                _          => null,
            };
        }

        _runners.Add(runner);
    }

    /// <summary>Removes the runner with the given agent ID, if present.</summary>
    public void Unregister(Guid agentId)
        => _runners.RemoveAll(r => r.Manifest.Id == agentId);

    /// <summary>
    /// Starts a background loop for every registered runner.
    /// Returns immediately; loops run concurrently.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        foreach (var runner in _runners.ToList())
            _loops.Add(RunLoopAsync(runner, _cts.Token));

        return Task.CompletedTask;
    }

    /// <summary>Cancels all runner loops and waits for them to finish.</summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loops.Count > 0)
            await Task.WhenAll(_loops).ConfigureAwait(false);
    }

    // -- per-runner loop --

    private async Task RunLoopAsync(AgentRunner runner, CancellationToken ct)
    {
        try
        {
            var sched = runner.Manifest.Schedule;
            if (sched is null) return;

            switch (sched.Trigger)
            {
                case "startup":
                    await RunSafeAsync(runner, ct);
                    break;

                case "interval":
                {
                    var period = GetInterval(runner.Manifest);
                    using var timer = new PeriodicTimer(period);
                    while (await timer.WaitForNextTickAsync(ct))
                    {
                        if (runner.Status == AgentStatus.Running)
                        {
                            Console.Error.WriteLine(
                                $"[Scheduler] Skipping run for agent {runner.Manifest.Id} -- previous run still in progress.");
                            continue;
                        }
                        runner.NextRun = DateTime.UtcNow.Add(period);
                        await RunSafeAsync(runner, ct);
                    }
                    break;
                }

                case "cron":
                {
                    if (sched.Cron is null) break;
                    var crontab = CrontabSchedule.Parse(sched.Cron);
                    while (!ct.IsCancellationRequested)
                    {
                        var next  = crontab.GetNextOccurrence(DateTime.UtcNow);
                        var delay = next - DateTime.UtcNow;
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, ct);
                        if (runner.Status == AgentStatus.Running)
                        {
                            Console.Error.WriteLine(
                                $"[Scheduler] Skipping cron run for agent {runner.Manifest.Id} -- previous run still in progress.");
                            continue;
                        }
                        runner.NextRun = crontab.GetNextOccurrence(DateTime.UtcNow);
                        if (!ct.IsCancellationRequested)
                            await RunSafeAsync(runner, ct);
                    }
                    break;
                }

                case "window":
                    await RunWindowLoopAsync(runner, sched, ct);
                    break;

                case "event":
                    await RunEventLoopAsync(runner, sched, ct);
                    break;
            }
        }
        catch (OperationCanceledException) { /* expected on StopAsync */ }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Scheduler] Loop error for agent {runner.Manifest.Id}: {ex.Message}");
        }
    }

    // -- window trigger --

    private static readonly TimeSpan WindowCloseLeadTime = TimeSpan.FromSeconds(60);

    private async Task RunWindowLoopAsync(AgentRunner runner, ScheduleBlock sched, CancellationToken ct)
    {
        var window = sched.Window;
        if (window is null) return;

        var runAt = window.RunAt;

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var (windowStart, windowEnd) = GetNextWindow(window, now);

            if (windowStart is null || windowEnd is null)
                break;

            switch (runAt)
            {
                case "window_open":
                {
                    var delay = windowStart.Value - now;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);

                    if (!ct.IsCancellationRequested && runner.Status != AgentStatus.Running)
                    {
                        runner.NextRun = IsCronExpression(window.Start)
                            ? GetNextWindow(window, DateTime.UtcNow.AddSeconds(1)).Start
                            : null;
                        await RunSafeAsync(runner, ct);
                    }

                    if (!IsCronExpression(window.Start))
                        return;

                    // Wait past this window before computing next
                    var pastEnd = windowEnd.Value - DateTime.UtcNow;
                    if (pastEnd > TimeSpan.Zero)
                        await Task.Delay(pastEnd, ct);
                    break;
                }

                case "window_close":
                {
                    var closeTime = windowEnd.Value - WindowCloseLeadTime;
                    var delay = closeTime - now;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);

                    if (!ct.IsCancellationRequested && runner.Status != AgentStatus.Running)
                    {
                        await RunSafeAsync(runner, ct);
                    }

                    if (!IsCronExpression(window.Start))
                        return;

                    var pastEnd = windowEnd.Value - DateTime.UtcNow;
                    if (pastEnd > TimeSpan.Zero)
                        await Task.Delay(pastEnd, ct);
                    break;
                }

                case "interval_within":
                {
                    var period = GetInterval(runner.Manifest);

                    // Wait for window to open
                    var delayToOpen = windowStart.Value - now;
                    if (delayToOpen > TimeSpan.Zero)
                        await Task.Delay(delayToOpen, ct);

                    // Fire on interval while inside window
                    while (!ct.IsCancellationRequested)
                    {
                        var current = DateTime.UtcNow;
                        if (current >= windowEnd.Value)
                            break; // window closed, no catch-up

                        if (runner.Status != AgentStatus.Running)
                        {
                            runner.NextRun = current.Add(period);
                            if (runner.NextRun > windowEnd.Value)
                                runner.NextRun = null;
                            await RunSafeAsync(runner, ct);
                        }

                        var nextTick = DateTime.UtcNow.Add(period);
                        var remaining = nextTick - DateTime.UtcNow;
                        var untilEnd = windowEnd.Value - DateTime.UtcNow;
                        if (remaining > untilEnd)
                            break;
                        if (remaining > TimeSpan.Zero)
                            await Task.Delay(remaining, ct);
                    }

                    if (!IsCronExpression(window.Start))
                        return;
                    break;
                }
            }
        }
    }

    private DateTime? ComputeNextWindowRun(ScheduleBlock sched)
    {
        if (sched.Window is not { } window) return null;
        var now = DateTime.UtcNow;
        var (start, end) = GetNextWindow(window, now);
        if (start is null) return null;

        return window.RunAt switch
        {
            "window_open"      => start,
            "window_close"     => end is { } e ? e - WindowCloseLeadTime : null,
            "interval_within"  => start,
            _                  => null,
        };
    }

    private static (DateTime? Start, DateTime? End) GetNextWindow(WindowBlock window, DateTime now)
    {
        DateTime? start, end;

        if (IsCronExpression(window.Start))
        {
            var startCron = CrontabSchedule.Parse(window.Start);
            var endCron   = CrontabSchedule.Parse(window.End);
            start = startCron.GetNextOccurrence(now);
            end   = endCron.GetNextOccurrence(start.Value);
        }
        else
        {
            start = DateTime.Parse(window.Start).ToUniversalTime();
            end   = DateTime.Parse(window.End).ToUniversalTime();
            if (end <= now)
            {
                start = null;
                end   = null;
            }
        }

        return (start, end);
    }

    private static bool IsCronExpression(string value)
    {
        // Cron has spaces between fields (min 4 spaces for 5-field cron)
        var spaceCount = 0;
        foreach (var c in value)
            if (c == ' ') spaceCount++;
        return spaceCount >= 4;
    }

    // -- event trigger --

    private async Task RunEventLoopAsync(AgentRunner runner, ScheduleBlock sched, CancellationToken ct)
    {
        var evt = sched.Event;
        if (evt is null) return;

        if (_eventSourceFactory is null)
        {
            Console.Error.WriteLine(
                $"[Scheduler] No event source factory configured -- cannot run event trigger for agent {runner.Manifest.Id}");
            return;
        }

        var source = _eventSourceFactory.Create(evt.Source, evt.Topic);
        var eventSignal = new SemaphoreSlim(0, 2);
        var queuedCount = 0;

        source.OnEvent += () =>
        {
            if (runner.Status == AgentStatus.Running)
            {
                var queued = Interlocked.CompareExchange(ref queuedCount, 1, 0);
                if (queued == 0)
                {
                    eventSignal.Release();
                    Console.Error.WriteLine(
                        $"[Scheduler] Agent {runner.Manifest.Id} busy -- event queued (depth 1).");
                }
                else
                {
                    Console.Error.WriteLine(
                        $"[Scheduler] Agent {runner.Manifest.Id} busy -- event dropped (queue full).");
                }
                return;
            }
            eventSignal.Release();
        };

        try
        {
            await source.ConnectAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                await eventSignal.WaitAsync(ct);
                Interlocked.Exchange(ref queuedCount, 0);

                if (runner.Status != AgentStatus.Running)
                    await RunSafeAsync(runner, ct);
            }
        }
        finally
        {
            await source.DisconnectAsync();
        }
    }

    // -- helpers --

    private static async Task RunSafeAsync(AgentRunner runner, CancellationToken ct)
    {
        try
        {
            await runner.RunOnceAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Scheduler] Agent {runner.Manifest.Id} crashed during run: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private TimeSpan GetInterval(AgentManifest manifest)
        => _intervalFactory?.Invoke(manifest)
           ?? TimeSpan.FromMinutes(manifest.Schedule?.IntervalMinutes ?? 15);
}

/// <summary>
/// Abstraction for connecting to an MCP event source.
/// </summary>
public interface IEventSource
{
    event Action? OnEvent;
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
}

/// <summary>
/// Factory for creating event source connections. Injected into the Scheduler.
/// </summary>
public interface IEventSourceFactory
{
    IEventSource Create(string serverUrl, string topic);
}
