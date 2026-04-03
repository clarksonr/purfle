using System.Text.Json;
using Purfle.Runtime.Scheduling;
using Purfle.Runtime.TokenUsage;

namespace Purfle.IntegrationTests;

public sealed class TokenUsageAccumulationTest : IDisposable
{
    private readonly string _tempDir;

    public TokenUsageAccumulationTest()
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
    public async Task TwoRuns_AccumulateTokenUsage()
    {
        // Arrange
        var factory = new ManifestTestFactory();
        var json = factory.BuildSignedJson();
        var manifest = System.Text.Json.JsonSerializer.Deserialize<Purfle.Runtime.Manifest.AgentManifest>(json);

        var tracker = new FileTokenUsageTracker(_tempDir);
        var mockAdapter = new MockLlmAdapter(inputTokens: 100, outputTokens: 50);
        var runner = new AgentRunner(manifest, mockAdapter, tokenUsageTracker: tracker);

        // Act — run twice
        await runner.RunOnceAsync();
        await runner.RunOnceAsync();

        // Assert — usage.jsonl has exactly 2 records
        var usagePath = Path.Combine(_tempDir, manifest.Id.ToString(), "usage.jsonl");
        Assert.True(File.Exists(usagePath), "usage.jsonl should exist");

        var lines = (await File.ReadAllLinesAsync(usagePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();
        Assert.Equal(2, lines.Length);

        foreach (var line in lines)
        {
            var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            Assert.Equal(manifest.Id.ToString(), root.GetProperty("agent_id").GetString());
            Assert.Equal(100, root.GetProperty("prompt_tokens").GetInt32());
            Assert.Equal(50, root.GetProperty("completion_tokens").GetInt32());
            Assert.Equal(150, root.GetProperty("total_tokens").GetInt32());
        }
    }
}
