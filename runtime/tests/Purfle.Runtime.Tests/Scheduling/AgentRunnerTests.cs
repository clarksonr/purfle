using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Scheduling;

namespace Purfle.Runtime.Tests.Scheduling;

public sealed class AgentRunnerTests
{
    // ── CompleteAsync call verification ───────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_UsesManifestNameInSystemPrompt()
    {
        var manifest = MakeManifest("EmailWatcher", "Watches your inbox");
        var adapter  = new FakeLlmAdapter("ok");
        var runner   = new AgentRunner(manifest, adapter);

        await runner.RunOnceAsync();

        Assert.Single(adapter.Calls);
        Assert.Contains("EmailWatcher", adapter.Calls[0].SystemPrompt);
    }

    [Fact]
    public async Task RunOnceAsync_UserMessage_ContainsTriggerTimestampAndInstruction()
    {
        var adapter = new FakeLlmAdapter("ok");
        var runner  = new AgentRunner(MakeManifest(), adapter);

        var before = DateTime.UtcNow;
        await runner.RunOnceAsync();
        var after = DateTime.UtcNow;

        var msg = adapter.Calls[0].UserMessage;
        Assert.Contains("You have been triggered at", msg);
        Assert.Contains("Perform your task", msg);

        // Timestamp in the message should be parseable and in range.
        var tsStart = msg.IndexOf("triggered at ", StringComparison.Ordinal) + "triggered at ".Length;
        var tsStr   = msg[tsStart..].Split(' ')[0].TrimEnd('.');
        var ts      = DateTime.Parse(tsStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.InRange(ts, before.AddSeconds(-1), after.AddSeconds(1));
    }

    // ── run.log output ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_WritesTimestampedEntryToRunLog()
    {
        var adapter = new FakeLlmAdapter("summary output");
        var runner  = new AgentRunner(MakeManifest(), adapter);

        await runner.RunOnceAsync();

        var logPath = Path.Combine(runner.OutputPath, "run.log");
        Assert.True(File.Exists(logPath));
        var content = await File.ReadAllTextAsync(logPath);
        Assert.Contains("===", content);
        Assert.Contains("summary output", content);
    }

    // ── Status / LastRun ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_SetsStatusIdleAndLastRunAfterSuccess()
    {
        var adapter = new FakeLlmAdapter("done");
        var runner  = new AgentRunner(MakeManifest(), adapter);

        Assert.Equal(AgentStatus.Idle, runner.Status);
        Assert.Null(runner.LastRun);

        await runner.RunOnceAsync();

        Assert.Equal(AgentStatus.Idle, runner.Status);
        Assert.NotNull(runner.LastRun);
    }

    [Fact]
    public async Task RunOnceAsync_OnAdapterException_SetsStatusError_DoesNotRethrow()
    {
        var adapter = new FakeLlmAdapter(throws: true);
        var runner  = new AgentRunner(MakeManifest(), adapter);

        // must not throw
        await runner.RunOnceAsync();

        Assert.Equal(AgentStatus.Error, runner.Status);

        var logPath = Path.Combine(runner.OutputPath, "run.log");
        Assert.True(File.Exists(logPath));
        var content = await File.ReadAllTextAsync(logPath);
        Assert.Contains("ERROR", content);
    }

    // ── Token usage ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunOnceAsync_CapturesTokenUsageFromAdapter()
    {
        var adapter = new FakeLlmAdapter("done", inputTokens: 42, outputTokens: 17);
        var runner  = new AgentRunner(MakeManifest(), adapter);

        await runner.RunOnceAsync();

        Assert.Equal((42, 17), runner.LastTokenUsage);
    }

    [Fact]
    public async Task RunOnceAsync_WritesTokenUsageToStructuredLog()
    {
        var adapter = new FakeLlmAdapter("done", inputTokens: 100, outputTokens: 50);
        var runner  = new AgentRunner(MakeManifest(), adapter);

        await runner.RunOnceAsync();

        var logPath = Path.Combine(runner.OutputPath, "run.jsonl");
        Assert.True(File.Exists(logPath));
        var json = await File.ReadAllTextAsync(logPath);
        Assert.Contains("\"input_tokens\":100", json);
        Assert.Contains("\"output_tokens\":50", json);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static AgentManifest MakeManifest(
        string name = "TestAgent", string description = "Does testing")
        => new()
        {
            Purfle      = "0.1",
            Id          = Guid.NewGuid(),
            Name        = name,
            Version     = "1.0.0",
            Description = description,
            Identity    = new IdentityBlock
            {
                Author    = "tester",
                Email     = "test@example.com",
                KeyId     = "key-1",
                Algorithm = "ES256",
                IssuedAt  = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1),
            },
            Capabilities = [],
            Runtime = new RuntimeBlock { Requires = "purfle/0.1", Engine = "anthropic" },
        };

    private sealed record CallRecord(string SystemPrompt, string UserMessage);

    private sealed class FakeLlmAdapter : ILlmAdapter
    {
        private readonly bool   _throws;
        private readonly string _response;
        private readonly int    _inputTokens;
        private readonly int    _outputTokens;

        public List<CallRecord> Calls { get; } = [];

        public FakeLlmAdapter(string response = "ok", bool throws = false,
                              int inputTokens = 0, int outputTokens = 0)
        {
            _response     = response;
            _throws       = throws;
            _inputTokens  = inputTokens;
            _outputTokens = outputTokens;
        }

        public Task<LlmResult> CompleteAsync(string systemPrompt, string userMessage,
                                          CancellationToken ct = default)
        {
            if (_throws) throw new InvalidOperationException("Adapter failed");
            Calls.Add(new CallRecord(systemPrompt, userMessage));
            return Task.FromResult(new LlmResult(_response, _inputTokens, _outputTokens));
        }
    }
}
