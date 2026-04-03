using Purfle.Runtime.Sandbox;

namespace Purfle.IntegrationTests;

public sealed class SandboxEnforcementTests
{
    [Fact]
    public void FsWriteBlocked_OutsidePermission()
    {
        // Arrange — fs.write only allows "./output"
        var permissions = new Purfle.Runtime.Manifest.AgentPermissions
        {
            Filesystem = new Purfle.Runtime.Manifest.FilesystemPermissions
            {
                Write = ["./output/**"],
                Read = [],
            },
        };
        var sandbox = new AgentSandbox(permissions);

        // Act & Assert
        Assert.True(sandbox.CanWritePath("./output/file.txt"));
        Assert.False(sandbox.CanWritePath("./forbidden/secret.txt"));
    }

    [Fact]
    public void EnvReadBlocked_OutsidePermission()
    {
        var permissions = new Purfle.Runtime.Manifest.AgentPermissions
        {
            Environment = new Purfle.Runtime.Manifest.EnvironmentPermissions
            {
                Allow = ["ALLOWED_VAR"],
            },
        };
        var sandbox = new AgentSandbox(permissions);

        Assert.True(sandbox.CanReadEnv("ALLOWED_VAR"));
        Assert.False(sandbox.CanReadEnv("BLOCKED_VAR"));
    }

    [Fact]
    public void NetworkBlocked_WithoutCapability()
    {
        // No network permissions at all
        var sandbox = new AgentSandbox(new Purfle.Runtime.Manifest.AgentPermissions());

        Assert.False(sandbox.CanAccessUrl("https://api.example.com/data"));
    }

    [Fact]
    public void CapabilityNegotiation_MissingRequired_Fails()
    {
        var agentCaps = new List<string> { "llm.chat", "network.outbound" };
        var runtimeCaps = new HashSet<string> { "llm.chat" }; // no network.outbound

        var result = CapabilityNegotiator.Negotiate(agentCaps, runtimeCaps);

        Assert.False(result.Success);
        Assert.Contains("network.outbound", result.MissingRequired);
    }
}
