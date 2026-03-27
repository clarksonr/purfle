using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Tests.Sandbox;

public sealed class CapabilityNegotiatorTests
{
    private static readonly IReadOnlySet<string> s_fullRuntime = new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
        CapabilityNegotiator.WellKnown.WebSearch,
        CapabilityNegotiator.WellKnown.Filesystem,
        CapabilityNegotiator.WellKnown.McpTools,
    };

    private static AgentCapability Cap(string id, bool required) =>
        new() { Id = id, Required = required };

    [Fact]
    public void Negotiate_EmptyCapabilities_Succeeds()
    {
        var result = CapabilityNegotiator.Negotiate([], s_fullRuntime);

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
        Assert.Empty(result.MissingOptional);
    }

    [Fact]
    public void Negotiate_RequiredCapabilityPresent_Succeeds()
    {
        var caps = new[] { Cap(CapabilityNegotiator.WellKnown.WebSearch, required: true) };
        var result = CapabilityNegotiator.Negotiate(caps, s_fullRuntime);

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Negotiate_RequiredCapabilityAbsent_Fails()
    {
        var caps = new[] { Cap("code-execution", required: true) };
        var result = CapabilityNegotiator.Negotiate(caps, s_fullRuntime);

        Assert.False(result.Success);
        Assert.Single(result.MissingRequired);
        Assert.Equal("code-execution", result.MissingRequired[0].Id);
    }

    [Fact]
    public void Negotiate_OptionalCapabilityAbsent_SucceedsWithWarning()
    {
        var caps = new[] { Cap("text-to-speech", required: false) };
        var result = CapabilityNegotiator.Negotiate(caps, s_fullRuntime);

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
        Assert.Single(result.MissingOptional);
        Assert.Equal("text-to-speech", result.MissingOptional[0].Id);
    }

    [Fact]
    public void Negotiate_InferenceAlwaysSatisfied_EvenWhenNotInRuntimeSet()
    {
        var caps = new[] { Cap(CapabilityNegotiator.WellKnown.Inference, required: true) };
        var emptyRuntime = new HashSet<string>();

        var result = CapabilityNegotiator.Negotiate(caps, emptyRuntime);

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Negotiate_MultipleRequired_OneAbsent_ReportsOnlyAbsent()
    {
        var caps = new[]
        {
            Cap(CapabilityNegotiator.WellKnown.WebSearch, required: true),   // present
            Cap("code-execution", required: true),                             // absent
        };

        var result = CapabilityNegotiator.Negotiate(caps, s_fullRuntime);

        Assert.False(result.Success);
        Assert.Single(result.MissingRequired);
        Assert.Equal("code-execution", result.MissingRequired[0].Id);
    }

    [Fact]
    public void Negotiate_ThirdPartyNamespacedCapability_TreatedNormally()
    {
        var runtimeWithThirdParty = new HashSet<string>(s_fullRuntime)
        {
            "com.acme.custom-capability",
        };

        var caps = new[] { Cap("com.acme.custom-capability", required: true) };
        var result = CapabilityNegotiator.Negotiate(caps, runtimeWithThirdParty);

        Assert.True(result.Success);
    }
}
