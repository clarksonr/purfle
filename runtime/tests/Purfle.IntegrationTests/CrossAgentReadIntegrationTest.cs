using Purfle.Runtime.CrossAgent;
using Purfle.Runtime.Scheduling;

namespace Purfle.IntegrationTests;

public sealed class CrossAgentReadIntegrationTest : IDisposable
{
    private readonly string _tempDir;
    private const string WriterAgentId = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";
    private const string ReaderAgentId = "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb";

    public CrossAgentReadIntegrationTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "purfle-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReaderCanReadWriterOutput_ViaIAgentOutputReader()
    {
        // Arrange — write known content as the "writer" agent
        var writerOutputDir = Path.Combine(_tempDir, WriterAgentId);
        Directory.CreateDirectory(writerOutputDir);

        var writerContent = "This is the email summary from the writer agent.";
        var logContent = $"=== 2026-04-03T07:00:00Z ==={Environment.NewLine}{writerContent}{Environment.NewLine}";
        await File.WriteAllTextAsync(Path.Combine(writerOutputDir, "run.log"), logContent);

        // Create run.jsonl entry for the writer
        var jsonlEntry = """{"agent_id":"aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa","trigger_time":"2026-04-03T07:00:00Z","status":"success","duration_ms":500}""";
        await File.WriteAllTextAsync(Path.Combine(writerOutputDir, "run.jsonl"), jsonlEntry + Environment.NewLine);

        // Create a sandboxed reader for the "reader" agent that can read the writer
        var allowedReads = new HashSet<string> { WriterAgentId };
        var reader = new AgentSandboxedOutputReader(ReaderAgentId, allowedReads, _tempDir);

        // Act — read the writer's latest output
        var latest = await reader.ReadLatestAsync(WriterAgentId);

        // Assert
        Assert.NotNull(latest);
        Assert.Contains("email summary from the writer agent", latest);

        // Also verify history works
        var history = await reader.ReadHistoryAsync(WriterAgentId, 10);
        Assert.Single(history);
        Assert.Equal(AgentOutputStatus.Success, history[0].Status);
    }

    [Fact]
    public async Task ReaderCannotReadUndeclaredAgent()
    {
        // Reader only declares writer as a dependency — should not be able to read random other agents
        var allowedReads = new HashSet<string> { WriterAgentId };
        var reader = new AgentSandboxedOutputReader(ReaderAgentId, allowedReads, _tempDir);

        var ex = await Assert.ThrowsAsync<AgentSandboxViolationException>(
            () => reader.ReadLatestAsync("cccccccc-cccc-4ccc-cccc-cccccccccccc"));

        Assert.Contains("io.reads", ex.Message);
    }
}
