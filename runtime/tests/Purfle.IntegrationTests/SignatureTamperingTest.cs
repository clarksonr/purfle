using Purfle.Runtime;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Sandbox;

namespace Purfle.IntegrationTests;

public sealed class SignatureTamperingTest
{
    [Fact]
    public async Task TamperedManifest_FailsVerification()
    {
        // Arrange
        var factory = new ManifestTestFactory();
        var registry = factory.CreateRegistry();
        var verifier = new IdentityVerifier(registry);

        var caps = new HashSet<string>
        {
            CapabilityNegotiator.WellKnown.LlmChat,
            CapabilityNegotiator.WellKnown.FsRead,
            CapabilityNegotiator.WellKnown.FsWrite,
            CapabilityNegotiator.WellKnown.NetworkOutbound,
            CapabilityNegotiator.WellKnown.EnvRead,
            CapabilityNegotiator.WellKnown.McpTool,
            CapabilityNegotiator.WellKnown.AgentRead,
        };

        var loader = new AgentLoader(verifier, caps);

        // Build a valid manifest, then tamper with it
        var tamperedJson = factory.BuildTamperedJson();

        // Act
        var result = await loader.LoadAsync(tamperedJson);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(LoadFailureReason.SignatureInvalid, result.FailureReason);
    }
}
