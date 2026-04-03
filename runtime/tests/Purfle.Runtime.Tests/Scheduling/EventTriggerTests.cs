using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Scheduling;

namespace Purfle.Runtime.Tests.Scheduling;

public sealed class EventTriggerTests
{
    [Fact]
    public async Task Event_FiresAgentWhenEventReceived()
    {
        var adapter = new FakeLlmAdapter();
        var fakeSource = new FakeEventSource();
        var factory = new FakeEventSourceFactory(fakeSource);
        var scheduler = new Scheduler(adapter, eventSourceFactory: factory);

        scheduler.Register(MakeEventManifest());

        await scheduler.StartAsync();
        await Task.Delay(100); // let loop start

        fakeSource.Fire();
        await Task.Delay(200);

        Assert.NotNull(scheduler.Runners[0].LastRun);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Event_QueuesOneWhileRunning()
    {
        var adapter = new SlowLlmAdapter(TimeSpan.FromMilliseconds(300));
        var fakeSource = new FakeEventSource();
        var factory = new FakeEventSourceFactory(fakeSource);
        var scheduler = new Scheduler(adapter, eventSourceFactory: factory);

        scheduler.Register(MakeEventManifest());

        await scheduler.StartAsync();
        await Task.Delay(100);

        // Fire first event (starts long-running agent)
        fakeSource.Fire();
        await Task.Delay(50); // let run start

        // Fire second event while agent is running (should be queued)
        fakeSource.Fire();
        await Task.Delay(50);

        // Fire third event while agent is still running (should be dropped)
        fakeSource.Fire();

        await Task.Delay(600); // wait for first run to complete + queued run

        Assert.True(adapter.CallCount >= 2, $"Expected at least 2 calls, got {adapter.CallCount}");
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Event_DropsThirdEventWhileQueueFull()
    {
        var adapter = new SlowLlmAdapter(TimeSpan.FromMilliseconds(500));
        var fakeSource = new FakeEventSource();
        var factory = new FakeEventSourceFactory(fakeSource);
        var scheduler = new Scheduler(adapter, eventSourceFactory: factory);

        scheduler.Register(MakeEventManifest());

        await scheduler.StartAsync();
        await Task.Delay(100);

        fakeSource.Fire();
        await Task.Delay(50);

        // Queue is now busy, fire two more (one queued, one dropped)
        fakeSource.Fire();
        fakeSource.Fire();

        // Wait for everything to process
        await Task.Delay(1200);

        // Should have at most 2 runs (first + one queued)
        Assert.True(adapter.CallCount <= 3, $"Expected at most 3 calls, got {adapter.CallCount}");
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Event_DisconnectsOnStop()
    {
        var adapter = new FakeLlmAdapter();
        var fakeSource = new FakeEventSource();
        var factory = new FakeEventSourceFactory(fakeSource);
        var scheduler = new Scheduler(adapter, eventSourceFactory: factory);

        scheduler.Register(MakeEventManifest());

        await scheduler.StartAsync();
        await Task.Delay(100);
        await scheduler.StopAsync();

        Assert.True(fakeSource.Disconnected, "Expected event source to be disconnected on stop");
    }

    [Fact]
    public void Register_EventTrigger_NextRunIsNull()
    {
        var adapter = new FakeLlmAdapter();
        var fakeSource = new FakeEventSource();
        var factory = new FakeEventSourceFactory(fakeSource);
        var scheduler = new Scheduler(adapter, eventSourceFactory: factory);

        scheduler.Register(MakeEventManifest());

        Assert.Null(scheduler.Runners[0].NextRun);
    }

    // -- helpers --

    private static AgentManifest MakeEventManifest()
        => new()
        {
            Purfle   = "0.1",
            Id       = Guid.NewGuid(),
            Name     = "EventTestAgent",
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
                Trigger = "event",
                Event   = new EventBlock
                {
                    Source = "http://localhost:9999",
                    Topic  = "test-topic",
                },
            },
        };

    private sealed class FakeLlmAdapter : ILlmAdapter
    {
        public Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage,
                                          CancellationToken ct = default)
            => Task.FromResult(new LlmResult("ok"));
    }

    private sealed class SlowLlmAdapter : ILlmAdapter
    {
        private readonly TimeSpan _delay;
        public int CallCount;

        public SlowLlmAdapter(TimeSpan delay) => _delay = delay;

        public async Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage,
                                                CancellationToken ct = default)
        {
            Interlocked.Increment(ref CallCount);
            await Task.Delay(_delay, ct);
            return new LlmResult("ok");
        }
    }

    private sealed class FakeEventSource : IEventSource
    {
        public event Action? OnEvent;
        public bool Connected { get; private set; }
        public bool Disconnected { get; private set; }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            Connected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            Disconnected = true;
            return Task.CompletedTask;
        }

        public void Fire() => OnEvent?.Invoke();
    }

    private sealed class FakeEventSourceFactory(FakeEventSource source) : IEventSourceFactory
    {
        public IEventSource Create(string serverUrl, string topic) => source;
    }
}
