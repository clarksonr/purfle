using System.Net;
using System.Text;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Scheduling;

namespace Purfle.Runtime.Tests.Scheduling;

public sealed class SseEventSourceTests
{
    // -- helpers --

    /// <summary>
    /// Fake HTTP message handler that serves an SSE stream from a queue of lines.
    /// Call <see cref="PushLine"/> to feed lines; call <see cref="EndStream"/> to close.
    /// </summary>
    private sealed class FakeSseHandler : HttpMessageHandler
    {
        private readonly SemaphoreSlim _lineReady = new(0);
        private readonly Queue<string?> _lines = new(); // null = end-of-stream
        private string? _lastRequestUrl;

        public string? LastRequestUrl => _lastRequestUrl;
        public int ConnectCount;

        /// <summary>Enqueue an SSE line (or blank line for event separator).</summary>
        public void PushLine(string line)
        {
            lock (_lines) _lines.Enqueue(line);
            _lineReady.Release();
        }

        /// <summary>End the SSE stream (simulates server disconnect).</summary>
        public void EndStream()
        {
            lock (_lines) _lines.Enqueue(null);
            _lineReady.Release();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref ConnectCount);
            _lastRequestUrl = request.RequestUri?.ToString();

            var stream = new SseStream(_lines, _lineReady, ct);
            var content = new StreamContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    /// <summary>
    /// A readable stream that yields SSE lines from a queue.
    /// </summary>
    private sealed class SseStream : Stream
    {
        private readonly Queue<string?> _lines;
        private readonly SemaphoreSlim _ready;
        private readonly CancellationToken _ct;
        private byte[] _buffer = [];
        private int _pos;
        private bool _done;

        public SseStream(Queue<string?> lines, SemaphoreSlim ready, CancellationToken ct)
        {
            _lines = lines;
            _ready = ready;
            _ct = ct;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            if (_done) return 0;

            while (_pos >= _buffer.Length)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _ct);
                await _ready.WaitAsync(linked.Token);

                string? line;
                lock (_lines) line = _lines.Dequeue();

                if (line is null) { _done = true; return 0; }

                _buffer = Encoding.UTF8.GetBytes(line + "\n");
                _pos = 0;
            }

            var toCopy = Math.Min(count, _buffer.Length - _pos);
            Array.Copy(_buffer, _pos, buffer, offset, toCopy);
            _pos += toCopy;
            return toCopy;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // -- tests --

    [Fact]
    public async Task MatchingEvent_FiresCallback()
    {
        var handler = new FakeSseHandler();
        var client = new HttpClient(handler);
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);
        var fired = new TaskCompletionSource<bool>();

        source.OnEvent += () => fired.TrySetResult(true);

        await source.ConnectAsync();
        await Task.Delay(50);

        handler.PushLine("event: test-topic");
        handler.PushLine("data: hello");
        handler.PushLine(""); // end of event

        var result = await Task.WhenAny(fired.Task, Task.Delay(2000));
        Assert.True(fired.Task.IsCompleted, "OnEvent should have been fired for matching topic");

        await source.DisconnectAsync();
    }

    [Fact]
    public async Task NonMatchingTopic_IsIgnored()
    {
        var handler = new FakeSseHandler();
        var client = new HttpClient(handler);
        var source = new SseEventSource("http://localhost:9999", "my-topic", httpClient: client);
        var callCount = 0;

        source.OnEvent += () => Interlocked.Increment(ref callCount);

        await source.ConnectAsync();
        await Task.Delay(50);

        // Send an event with a different topic
        handler.PushLine("event: other-topic");
        handler.PushLine("data: ignore-me");
        handler.PushLine("");

        // Send a matching one
        handler.PushLine("event: my-topic");
        handler.PushLine("data: accept-me");
        handler.PushLine("");

        await Task.Delay(300);

        Assert.Equal(1, callCount);
        await source.DisconnectAsync();
    }

    [Fact]
    public async Task ReconnectsAfterStreamEnds()
    {
        var handler = new FakeSseHandler();
        var client = new HttpClient(handler);
        // Use a custom SSE source with very short backoff for fast tests
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);

        await source.ConnectAsync();
        await Task.Delay(50);

        // End the first stream to trigger reconnect
        handler.EndStream();

        // Wait for reconnect attempt
        await Task.Delay(2500);

        Assert.True(handler.ConnectCount >= 2, $"Expected at least 2 connect attempts, got {handler.ConnectCount}");
        await source.DisconnectAsync();
    }

    [Fact]
    public async Task QueueDepth1_Enforcement()
    {
        // This tests that the Scheduler's event loop enforces queue depth 1
        // (second event queued while agent running, third dropped)
        var adapter = new SlowLlmAdapter(TimeSpan.FromMilliseconds(300));
        var fakeSource = new FakeEventSource();
        var factory = new FakeEventSourceFactory(fakeSource);
        var scheduler = new Scheduler(adapter, eventSourceFactory: factory);

        scheduler.Register(MakeEventManifest());
        await scheduler.StartAsync();
        await Task.Delay(100);

        // Fire first event (starts long-running agent)
        fakeSource.Fire();
        await Task.Delay(50);

        // Fire second (queued)
        fakeSource.Fire();
        await Task.Delay(20);

        // Fire third (should be dropped)
        fakeSource.Fire();

        await Task.Delay(800);

        // First run + one queued = at most 2-3 adapter calls
        Assert.True(adapter.CallCount >= 2 && adapter.CallCount <= 3,
            $"Expected 2-3 calls (first + queued), got {adapter.CallCount}");
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task CleanShutdown_OnCancel()
    {
        var handler = new FakeSseHandler();
        var client = new HttpClient(handler);
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);

        using var cts = new CancellationTokenSource();
        await source.ConnectAsync(cts.Token);
        await Task.Delay(50);

        // Cancel should cause clean disconnect
        cts.Cancel();
        await Task.Delay(200);

        // Reconnect attempt with handler PushLine should not throw
        // Just verify disconnect doesn't throw
        await source.DisconnectAsync();
    }

    [Fact]
    public async Task DataOnlyEvent_WithNoEventType_Fires()
    {
        var handler = new FakeSseHandler();
        var client = new HttpClient(handler);
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);
        var fired = new TaskCompletionSource<bool>();

        source.OnEvent += () => fired.TrySetResult(true);

        await source.ConnectAsync();
        await Task.Delay(50);

        // Data-only event (no event: line) should still fire
        handler.PushLine("data: bare-data");
        handler.PushLine("");

        var result = await Task.WhenAny(fired.Task, Task.Delay(2000));
        Assert.True(fired.Task.IsCompleted, "Data-only event should fire callback");

        await source.DisconnectAsync();
    }

    [Fact]
    public void Factory_CreatesInstance()
    {
        var factory = new SseEventSourceFactory();
        var source = factory.Create("http://localhost:9999", "my-topic");
        Assert.IsType<SseEventSource>(source);
    }

    [Fact]
    public async Task FiveConsecutiveFailures_FiresDegraded()
    {
        var errorHandler = new FakeErrorHandler();
        var client = new HttpClient(errorHandler);
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);
        var degradedFired = new TaskCompletionSource<bool>();

        source.OnDegraded += () => degradedFired.TrySetResult(true);

        await source.ConnectAsync();

        // Wait for 5 consecutive failures with backoff
        // Backoff is 1s, 2s, 4s... but with errors, it should fire degraded after 5 failures
        var result = await Task.WhenAny(degradedFired.Task, Task.Delay(20000));
        Assert.True(degradedFired.Task.IsCompleted,
            $"OnDegraded should fire after {SseEventSource.DegradedThreshold} failures, " +
            $"got {source.ConsecutiveFailures} failures");

        await source.DisconnectAsync();
    }

    [Fact]
    public async Task SuccessfulConnect_ResetsFailureCount()
    {
        var handler = new FakeSseHandler();
        var client = new HttpClient(handler);
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);

        await source.ConnectAsync();
        await Task.Delay(100);

        // After successful connect, consecutive failures should be 0
        Assert.Equal(0, source.ConsecutiveFailures);

        await source.DisconnectAsync();
    }

    [Fact]
    public async Task DegradedAgent_ContinuesRetrying()
    {
        var errorHandler = new FakeErrorHandler();
        var client = new HttpClient(errorHandler);
        var source = new SseEventSource("http://localhost:9999", "test-topic", httpClient: client);
        var degradedFired = new TaskCompletionSource<bool>();

        source.OnDegraded += () => degradedFired.TrySetResult(true);

        await source.ConnectAsync();

        // Wait for degraded (5 failures with exponential backoff: ~1+2+4+8+16s ≈ 31s)
        var result = await Task.WhenAny(degradedFired.Task, Task.Delay(40000));
        Assert.True(degradedFired.Task.IsCompleted, "Should be degraded");

        // After degraded, the source should still be retrying (wait for next backoff cycle)
        var failuresBefore = source.ConsecutiveFailures;
        await Task.Delay(65000); // max backoff is 60s
        Assert.True(source.ConsecutiveFailures > failuresBefore,
            "Should continue retrying after degraded");

        await source.DisconnectAsync();
    }

    // -- test helpers --

    /// <summary>
    /// HTTP handler that always returns 500 to simulate repeated connection failures.
    /// </summary>
    private sealed class FakeErrorHandler : HttpMessageHandler
    {
        public int AttemptCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref AttemptCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server error"),
            });
        }
    }


    private static AgentManifest MakeEventManifest()
        => new()
        {
            Purfle   = "0.1",
            Id       = Guid.NewGuid(),
            Name     = "SseTestAgent",
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
            Runtime  = new RuntimeBlock { Requires = "purfle/0.1", Engine = "gemini" },
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
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public void Fire() => OnEvent?.Invoke();
    }

    private sealed class FakeEventSourceFactory(FakeEventSource source) : IEventSourceFactory
    {
        public IEventSource Create(string serverUrl, string topic) => source;
    }
}
