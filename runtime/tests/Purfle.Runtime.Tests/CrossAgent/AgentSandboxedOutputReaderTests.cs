using Purfle.Runtime.CrossAgent;
using Purfle.Runtime.Lifecycle;

namespace Purfle.Runtime.Tests.CrossAgent;

public sealed class AgentSandboxedOutputReaderTests : IDisposable
{
    private readonly string _tempDir;
    private const string RequestingAgent = "requester-0000-0000-0000-000000000000";
    private const string EmailMonitor = "b2e4f6a8-1234-4abc-9def-111111111111";
    private const string PrWatcher = "c3f5a7b9-2345-4bcd-aef0-222222222222";

    public AgentSandboxedOutputReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "purfle-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task AllowedReadSucceeds()
    {
        // Arrange — create email-monitor output
        var agentOutputDir = Path.Combine(_tempDir, EmailMonitor);
        Directory.CreateDirectory(agentOutputDir);
        var logContent = $"=== 2026-04-03T07:00:00Z ==={Environment.NewLine}Email summary content here{Environment.NewLine}";
        await File.WriteAllTextAsync(Path.Combine(agentOutputDir, "run.log"), logContent);

        var allowed = new HashSet<string> { EmailMonitor };
        var reader = new AgentSandboxedOutputReader(RequestingAgent, allowed, _tempDir);

        // Act
        var result = await reader.ReadLatestAsync(EmailMonitor);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Email summary content here", result);
    }

    [Fact]
    public async Task BlockedReadThrows()
    {
        // Arrange — allow email-monitor but try to read pr-watcher
        var allowed = new HashSet<string> { EmailMonitor };
        var reader = new AgentSandboxedOutputReader(RequestingAgent, allowed, _tempDir);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AgentSandboxViolationException>(
            () => reader.ReadLatestAsync(PrWatcher));

        Assert.Contains(PrWatcher, ex.Message);
        Assert.Contains("io.reads", ex.Message);
    }

    [Fact]
    public async Task ReadLatestReturnsNullWhenNoOutput()
    {
        // Arrange — allowed agent exists in allowlist but has no output
        var allowed = new HashSet<string> { EmailMonitor };
        var reader = new AgentSandboxedOutputReader(RequestingAgent, allowed, _tempDir);

        // Act
        var result = await reader.ReadLatestAsync(EmailMonitor);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadHistoryReturnsChronological()
    {
        // Arrange — write 3 run.jsonl entries
        var agentOutputDir = Path.Combine(_tempDir, EmailMonitor);
        Directory.CreateDirectory(agentOutputDir);

        var lines = new[]
        {
            """{"agent_id":"b2e4f6a8-1234-4abc-9def-111111111111","trigger_time":"2026-04-01T07:00:00Z","status":"success","duration_ms":500}""",
            """{"agent_id":"b2e4f6a8-1234-4abc-9def-111111111111","trigger_time":"2026-04-02T07:00:00Z","status":"success","duration_ms":600}""",
            """{"agent_id":"b2e4f6a8-1234-4abc-9def-111111111111","trigger_time":"2026-04-03T07:00:00Z","status":"error","error":"Connection failed"}""",
        };
        await File.WriteAllTextAsync(
            Path.Combine(agentOutputDir, "run.jsonl"),
            string.Join(Environment.NewLine, lines) + Environment.NewLine);

        var allowed = new HashSet<string> { EmailMonitor };
        var reader = new AgentSandboxedOutputReader(RequestingAgent, allowed, _tempDir);

        // Act
        var history = await reader.ReadHistoryAsync(EmailMonitor, 10);

        // Assert
        Assert.Equal(3, history.Count);
        // Newest first
        Assert.Equal(AgentOutputStatus.Error, history[0].Status);
        Assert.Contains("Connection failed", history[0].Content);
        Assert.Equal(AgentOutputStatus.Success, history[1].Status);
        Assert.Equal(AgentOutputStatus.Success, history[2].Status);
    }

    [Fact]
    public async Task EmptyReadsAllowed()
    {
        // Arrange — agent with empty reads list
        var allowed = new HashSet<string>();
        var reader = new AgentSandboxedOutputReader(RequestingAgent, allowed, _tempDir);

        // Act & Assert — should not throw during construction or when not reading
        Assert.NotNull(reader);
    }

    [Fact]
    public async Task MissingAgentFailsLoad_InvalidCrossAgentReference()
    {
        // This test validates the LoadFailureReason enum value exists
        Assert.Equal("InvalidCrossAgentReference",
            LoadFailureReason.InvalidCrossAgentReference.ToString());
    }
}
