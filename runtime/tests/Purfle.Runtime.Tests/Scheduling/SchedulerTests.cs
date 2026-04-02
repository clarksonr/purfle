using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Scheduling;

namespace Purfle.Runtime.Tests.Scheduling;

public sealed class SchedulerTests
{
    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public void Register_AddsRunnerToRunners()
    {
        var scheduler = new Scheduler(new FakeLlmAdapter());
        scheduler.Register(MakeManifest());
        Assert.Single(scheduler.Runners);
    }

    [Fact]
    public void Register_MultipleManifests_AddsAllRunners()
    {
        var scheduler = new Scheduler(new FakeLlmAdapter());
        scheduler.Register(MakeManifest());
        scheduler.Register(MakeManifest());
        Assert.Equal(2, scheduler.Runners.Count);
    }

    // ── Startup trigger ───────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_StartupTrigger_RunnerExecutesOnce()
    {
        var adapter   = new FakeLlmAdapter();
        var scheduler = new Scheduler(adapter);
        scheduler.Register(MakeManifest(trigger: "startup"));

        await scheduler.StartAsync();
        await Task.Delay(200); // allow the background task to complete

        Assert.NotNull(scheduler.Runners[0].LastRun);
    }

    // ── Interval trigger ──────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_IntervalTrigger_RunnerExecutesAfterOneTick()
    {
        var adapter   = new FakeLlmAdapter();
        // Use a 50 ms interval so the test completes quickly.
        var scheduler = new Scheduler(adapter,
            intervalFactory: _ => TimeSpan.FromMilliseconds(50));
        scheduler.Register(MakeManifest(trigger: "interval", intervalMinutes: 1));

        await scheduler.StartAsync();
        await Task.Delay(300); // wait for at least one tick

        Assert.NotNull(scheduler.Runners[0].LastRun);

        await scheduler.StopAsync();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static AgentManifest MakeManifest(
        string trigger        = "startup",
        int    intervalMinutes = 1,
        string? cron          = null)
        => new()
        {
            Purfle   = "0.1",
            Id       = Guid.NewGuid(),
            Name     = "TestAgent",
            Version  = "1.0.0",
            Identity = new IdentityBlock
            {
                Author    = "tester",
                Email     = "test@example.com",
                KeyId     = "key-1",
                Algorithm = "ES256",
                IssuedAt  = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
            },
            Capabilities = [],
            Runtime  = new RuntimeBlock { Requires = "purfle/0.1", Engine = "anthropic" },
            Schedule = new ScheduleBlock
            {
                Trigger         = trigger,
                IntervalMinutes = trigger == "interval" ? intervalMinutes : null,
                Cron            = cron,
            },
        };

    private sealed class FakeLlmAdapter : ILlmAdapter
    {
        public Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage,
                                          CancellationToken ct = default)
            => Task.FromResult(new LlmResult("ok"));
    }
}
