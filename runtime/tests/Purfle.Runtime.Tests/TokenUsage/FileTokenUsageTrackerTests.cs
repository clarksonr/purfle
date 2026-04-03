using System.Text.Json;
using Purfle.Runtime.TokenUsage;

namespace Purfle.Runtime.Tests.TokenUsage;

public sealed class FileTokenUsageTrackerTests : IDisposable
{
    private readonly string _tempDir;

    public FileTokenUsageTrackerTests()
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
    public async Task RecordAppendsToFile()
    {
        var tracker = new FileTokenUsageTracker(_tempDir);
        var agentId = Guid.NewGuid().ToString();
        var ts = DateTimeOffset.UtcNow;

        await tracker.RecordAsync(agentId, "gemini", "gemini-2.0-flash", 100, 50, ts);

        var filePath = Path.Combine(_tempDir, agentId, "usage.jsonl");
        Assert.True(File.Exists(filePath));

        var lines = await File.ReadAllLinesAsync(filePath);
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.Single(nonEmpty);

        var record = JsonSerializer.Deserialize<TokenUsageRecord>(lines[0]);
        Assert.NotNull(record);
        Assert.Equal(agentId, record.AgentId);
        Assert.Equal("gemini", record.Engine);
        Assert.Equal("gemini-2.0-flash", record.Model);
        Assert.Equal(100, record.PromptTokens);
        Assert.Equal(50, record.CompletionTokens);
        Assert.Equal(150, record.TotalTokens);
        Assert.Equal(ts.ToString("O"), record.Timestamp);
    }

    [Fact]
    public async Task MultipleRecordsAccumulate()
    {
        var tracker = new FileTokenUsageTracker(_tempDir);
        var agentId = Guid.NewGuid().ToString();
        var ts = DateTimeOffset.UtcNow;

        await tracker.RecordAsync(agentId, "gemini", "gemini-2.0-flash", 10, 5, ts);
        await tracker.RecordAsync(agentId, "gemini", "gemini-2.0-flash", 20, 10, ts.AddMinutes(1));
        await tracker.RecordAsync(agentId, "gemini", "gemini-2.0-flash", 30, 15, ts.AddMinutes(2));

        var filePath = Path.Combine(_tempDir, agentId, "usage.jsonl");
        var lines = (await File.ReadAllLinesAsync(filePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.Equal(3, lines.Length);

        // Verify each line is valid JSON with correct token counts
        var records = lines.Select(l => JsonSerializer.Deserialize<TokenUsageRecord>(l)!).ToArray();
        Assert.Equal(10, records[0].PromptTokens);
        Assert.Equal(20, records[1].PromptTokens);
        Assert.Equal(30, records[2].PromptTokens);
        Assert.Equal(15, records[0].TotalTokens);
        Assert.Equal(30, records[1].TotalTokens);
        Assert.Equal(45, records[2].TotalTokens);
    }

    [Fact]
    public async Task ConcurrentWritesAreThreadSafe()
    {
        var tracker = new FileTokenUsageTracker(_tempDir);
        var agentId = Guid.NewGuid().ToString();
        var ts = DateTimeOffset.UtcNow;

        var tasks = Enumerable.Range(0, 10)
            .Select(i => tracker.RecordAsync(agentId, "openai", "gpt-4o", i * 10, i * 5, ts.AddSeconds(i)))
            .ToArray();

        await Task.WhenAll(tasks);

        var filePath = Path.Combine(_tempDir, agentId, "usage.jsonl");
        var lines = (await File.ReadAllLinesAsync(filePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.Equal(10, lines.Length);

        // Verify all lines are valid JSON
        foreach (var line in lines)
        {
            var record = JsonSerializer.Deserialize<TokenUsageRecord>(line);
            Assert.NotNull(record);
            Assert.Equal(agentId, record.AgentId);
            Assert.Equal("openai", record.Engine);
            Assert.Equal("gpt-4o", record.Model);
        }

        // Verify all 10 distinct prompt_token values are present
        var promptTokenValues = lines
            .Select(l => JsonSerializer.Deserialize<TokenUsageRecord>(l)!.PromptTokens)
            .OrderBy(v => v)
            .ToArray();

        var expected = Enumerable.Range(0, 10).Select(i => i * 10).ToArray();
        Assert.Equal(expected, promptTokenValues);
    }
}
