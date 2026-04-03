using System.Text.Json.Nodes;
using Purfle.Runtime;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Sandbox;

namespace Purfle.IntegrationTests;

public sealed class ExpiredManifestTest
{
    [Fact]
    public async Task ExpiredManifest_FailsLoad()
    {
        // Arrange
        var factory = new ManifestTestFactory();
        var registry = factory.CreateRegistry();
        var verifier = new IdentityVerifier(registry);
        var caps = new HashSet<string>
        {
            CapabilityNegotiator.WellKnown.LlmChat,
            CapabilityNegotiator.WellKnown.AgentRead,
        };

        var loader = new AgentLoader(verifier, caps);

        // Build a manifest that expired 1 second ago
        var expiredJson = factory.BuildSignedJson(node =>
        {
            var identity = node["identity"]!.AsObject();
            identity["expires_at"] = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O");
        });

        // Act
        var result = await loader.LoadAsync(expiredJson);

        // Assert
        Assert.False(result.Success);
        Assert.True(
            result.FailureReason == LoadFailureReason.ManifestExpired ||
            result.FailureReason == LoadFailureReason.IdentityExpired,
            $"Expected ManifestExpired or IdentityExpired, got {result.FailureReason}: {result.FailureMessage}");
    }
}
