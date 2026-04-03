using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Scheduling;

namespace Purfle.Runtime.Tests.Scheduling;

public sealed class WindowTriggerTests
{
    [Fact]
    public async Task WindowOpen_FiresWhenWindowOpens()
    {
        var adapter = new FakeLlmAdapter();
        var scheduler = new Scheduler(adapter,
            intervalFactory: _ => TimeSpan.FromMilliseconds(50));

        // Window opens in 100ms, closes in 2s
        var start = DateTime.UtcNow.AddMilliseconds(100);
        var end = DateTime.UtcNow.AddSeconds(2);

        scheduler.Register(MakeWindowManifest(
            start.ToString("O"), end.ToString("O"), "window_open"));

        await scheduler.StartAsync();
        await Task.Delay(500);

        Assert.NotNull(scheduler.Runners[0].LastRun);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task WindowOpen_DoesNotFireAfterWindowCloses()
    {
        var adapter = new FakeLlmAdapter();
        var scheduler = new Scheduler(adapter);

        // Window already closed
        var start = DateTime.UtcNow.AddSeconds(-10);
        var end = DateTime.UtcNow.AddSeconds(-5);

        scheduler.Register(MakeWindowManifest(
            start.ToString("O"), end.ToString("O"), "window_open"));

        await scheduler.StartAsync();
        await Task.Delay(200);

        Assert.Null(scheduler.Runners[0].LastRun);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task IntervalWithin_RunsInsideWindow_StopsOutside()
    {
        var adapter = new CountingLlmAdapter();
        var scheduler = new Scheduler(adapter,
            intervalFactory: _ => TimeSpan.FromMilliseconds(50));

        // Window opens now, closes in 300ms
        var start = DateTime.UtcNow.AddMilliseconds(-10);
        var end = DateTime.UtcNow.AddMilliseconds(300);

        scheduler.Register(MakeWindowManifest(
            start.ToString("O"), end.ToString("O"), "interval_within", intervalMinutes: 1));

        await scheduler.StartAsync();
        await Task.Delay(500);

        // Should have fired at least once but stopped after window
        Assert.True(adapter.CallCount > 0, "Expected at least one run inside window");
        var countAfterWindow = adapter.CallCount;

        // Wait more and verify no additional runs
        await Task.Delay(200);
        Assert.Equal(countAfterWindow, adapter.CallCount);

        await scheduler.StopAsync();
    }

    [Fact]
    public void Register_WindowTrigger_SetsNextRun()
    {
        var adapter = new FakeLlmAdapter();
        var scheduler = new Scheduler(adapter);

        var start = DateTime.UtcNow.AddMinutes(5);
        var end = DateTime.UtcNow.AddMinutes(10);

        scheduler.Register(MakeWindowManifest(
            start.ToString("O"), end.ToString("O"), "window_open"));

        Assert.NotNull(scheduler.Runners[0].NextRun);
    }

    [Fact]
    public async Task WindowClose_FiresBeforeWindowCloses()
    {
        var adapter = new FakeLlmAdapter();
        var scheduler = new Scheduler(adapter);

        // Window opens now, closes in 200ms (close lead time is 60s,
        // so with a short window the close fires almost immediately)
        var start = DateTime.UtcNow.AddMilliseconds(-10);
        var end = DateTime.UtcNow.AddMilliseconds(200);

        scheduler.Register(MakeWindowManifest(
            start.ToString("O"), end.ToString("O"), "window_close"));

        await scheduler.StartAsync();
        await Task.Delay(500);

        // With a short window (200ms < 60s lead time), the close time is before now
        // so it should fire immediately
        Assert.NotNull(scheduler.Runners[0].LastRun);
        await scheduler.StopAsync();
    }

    // -- helpers --

    private static AgentManifest MakeWindowManifest(
        string windowStart, string windowEnd, string runAt,
        int intervalMinutes = 1)
        => new()
        {
            Purfle   = "0.1",
            Id       = Guid.NewGuid(),
            Name     = "WindowTestAgent",
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
                Trigger         = "window",
                IntervalMinutes = intervalMinutes,
                Window          = new WindowBlock
                {
                    Start = windowStart,
                    End   = windowEnd,
                    RunAt = runAt,
                },
            },
        };

    private sealed class FakeLlmAdapter : ILlmAdapter
    {
        public Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage,
                                          CancellationToken ct = default)
            => Task.FromResult(new LlmResult("ok"));
    }

    private sealed class CountingLlmAdapter : ILlmAdapter
    {
        public int CallCount;

        public Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage,
                                          CancellationToken ct = default)
        {
            Interlocked.Increment(ref CallCount);
            return Task.FromResult(new LlmResult("ok"));
        }
    }
}
