using System.Text.Json.Nodes;
using Purfle.Runtime.Scheduling;

namespace Purfle.IntegrationTests;

public sealed class MultiAgentIsolationTest : IDisposable
{
    private readonly string _tempDir;

    public MultiAgentIsolationTest()
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
    public async Task TwoAgents_WriteToOwnDirectories_NoContamination()
    {
        var factory = new ManifestTestFactory();

        // Agent 1
        var json1 = factory.BuildSignedJson(node =>
        {
            node["id"] = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";
            node["name"] = "Agent One";
        });
        var manifest1 = System.Text.Json.JsonSerializer.Deserialize<Purfle.Runtime.Manifest.AgentManifest>(json1);
        var mock1 = new MockLlmAdapter("Output from Agent One");
        var runner1 = new AgentRunner(manifest1, mock1);

        // Agent 2
        var json2 = factory.BuildSignedJson(node =>
        {
            node["id"] = "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb";
            node["name"] = "Agent Two";
        });
        var manifest2 = System.Text.Json.JsonSerializer.Deserialize<Purfle.Runtime.Manifest.AgentManifest>(json2);
        var mock2 = new MockLlmAdapter("Output from Agent Two");
        var runner2 = new AgentRunner(manifest2, mock2);

        // Run both concurrently
        await Task.WhenAll(runner1.RunOnceAsync(), runner2.RunOnceAsync());

        // Assert — each has its own output
        Assert.Equal(AgentStatus.Idle, runner1.Status);
        Assert.Equal(AgentStatus.Idle, runner2.Status);

        var log1 = await File.ReadAllTextAsync(Path.Combine(runner1.OutputPath, "run.log"));
        var log2 = await File.ReadAllTextAsync(Path.Combine(runner2.OutputPath, "run.log"));

        Assert.Contains("Output from Agent One", log1);
        Assert.Contains("Output from Agent Two", log2);

        // No cross-contamination
        Assert.DoesNotContain("Agent Two", log1);
        Assert.DoesNotContain("Agent One", log2);
    }
}
