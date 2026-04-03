using Purfle.Runtime;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Sandbox;
using Purfle.Runtime.Scheduling;
using Purfle.Runtime.TokenUsage;

namespace Purfle.IntegrationTests;

public sealed class AgentLifecycleTest : IDisposable
{
    private readonly string _tempDir;

    public AgentLifecycleTest()
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
    public async Task LoadAndRunAgent_ProducesOutput()
    {
        // Arrange
        var factory = new ManifestTestFactory();
        var registry = factory.CreateRegistry();
        var verifier = new IdentityVerifier(registry);
        var caps = new HashSet<string>
        {
            CapabilityNegotiator.WellKnown.LlmChat,
            CapabilityNegotiator.WellKnown.LlmCompletion,
            CapabilityNegotiator.WellKnown.NetworkOutbound,
            CapabilityNegotiator.WellKnown.EnvRead,
            CapabilityNegotiator.WellKnown.FsRead,
            CapabilityNegotiator.WellKnown.FsWrite,
            CapabilityNegotiator.WellKnown.McpTool,
            CapabilityNegotiator.WellKnown.AgentRead,
        };

        var loader = new AgentLoader(verifier, caps);
        var json = factory.BuildSignedJson();

        // Act — load the agent
        var result = await loader.LoadAsync(json);

        // Assert — load succeeded
        Assert.True(result.Success, result.FailureMessage ?? "Load failed");
        Assert.NotNull(result.Manifest);
        Assert.Equal("Hello World", result.Manifest!.Name);

        // Run with mock adapter
        var mockAdapter = new MockLlmAdapter();
        var runner = new AgentRunner(result.Manifest, mockAdapter);
        await runner.RunOnceAsync();

        // Assert — output file was created
        Assert.Equal(AgentStatus.Idle, runner.Status);
        Assert.NotNull(runner.LastRun);
        Assert.True(Directory.Exists(runner.OutputPath), "Output directory should exist");
        var logPath = Path.Combine(runner.OutputPath, "run.log");
        Assert.True(File.Exists(logPath), "run.log should exist");
        var logContent = await File.ReadAllTextAsync(logPath);
        Assert.Contains("Hello from mock!", logContent);
    }
}
