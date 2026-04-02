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

    public IReadOnlyList<AgentRunner> Runners => _runners.AsReadOnly();

    /// <param name="llmAdapter">Adapter shared by all registered runners.</param>
    /// <param name="intervalFactory">
    /// Optional override for the interval computation. Defaults to
    /// <c>TimeSpan.FromMinutes(schedule.interval_minutes)</c>. Inject a shorter
    /// factory in tests to avoid wall-clock waits.
    /// </param>
    public Scheduler(ILlmAdapter llmAdapter,
                     Func<AgentManifest, TimeSpan>? intervalFactory = null)
    {
        _llmAdapter      = llmAdapter;
        _intervalFactory = intervalFactory;
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

    // ── per-runner loop ───────────────────────────────────────────────────────

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
                        // Skip overlapping runs — if agent is still running, skip this tick
                        if (runner.Status == AgentStatus.Running)
                        {
                            Console.Error.WriteLine(
                                $"[Scheduler] Skipping run for agent {runner.Manifest.Id} — previous run still in progress.");
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
                        // Skip if still running from previous cron trigger
                        if (runner.Status == AgentStatus.Running)
                        {
                            Console.Error.WriteLine(
                                $"[Scheduler] Skipping cron run for agent {runner.Manifest.Id} — previous run still in progress.");
                            continue;
                        }
                        runner.NextRun = crontab.GetNextOccurrence(DateTime.UtcNow);
                        if (!ct.IsCancellationRequested)
                            await RunSafeAsync(runner, ct);
                    }
                    break;
                }
            }
        }
        catch (OperationCanceledException) { /* expected on StopAsync */ }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Scheduler] Loop error for agent {runner.Manifest.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs an agent with crash isolation — any exception from the agent is caught
    /// and logged so the scheduler loop continues to reschedule normally.
    /// </summary>
    private static async Task RunSafeAsync(AgentRunner runner, CancellationToken ct)
    {
        try
        {
            await runner.RunOnceAsync(ct);
        }
        catch (OperationCanceledException) { throw; } // propagate cancellation
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Scheduler] Agent {runner.Manifest.Id} crashed during run: {ex.Message}\n{ex.StackTrace}");
            // AgentRunner.RunOnceAsync already catches exceptions, but if something
            // escapes (e.g. out of memory), we catch it here so the loop continues.
        }
    }

    private TimeSpan GetInterval(AgentManifest manifest)
        => _intervalFactory?.Invoke(manifest)
           ?? TimeSpan.FromMinutes(manifest.Schedule?.IntervalMinutes ?? 15);
}
