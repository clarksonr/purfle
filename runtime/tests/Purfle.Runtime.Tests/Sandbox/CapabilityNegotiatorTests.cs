using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Tests.Sandbox;

public sealed class CapabilityNegotiatorTests
{
    private static readonly IReadOnlySet<string> s_fullRuntime = new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
        CapabilityNegotiator.WellKnown.NetworkOutbound,
        CapabilityNegotiator.WellKnown.FsRead,
        CapabilityNegotiator.WellKnown.FsWrite,
        CapabilityNegotiator.WellKnown.McpTool,
    };

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
        var result = CapabilityNegotiator.Negotiate(
            [CapabilityNegotiator.WellKnown.NetworkOutbound],
            s_fullRuntime);

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Negotiate_RequiredCapabilityAbsent_Fails()
    {
        // "mcp.tool" is not in the runtime set (only inference/network.outbound/fs.*)
        var result = CapabilityNegotiator.Negotiate(
            ["mcp.tool"],
            new HashSet<string> { CapabilityNegotiator.WellKnown.Inference });

        Assert.False(result.Success);
        Assert.Single(result.MissingRequired);
        Assert.Equal("mcp.tool", result.MissingRequired[0]);
    }

    [Fact]
    public void Negotiate_LlmChat_IsAlwaysSatisfied()
    {
        // "llm.chat" is implicitly satisfied regardless of the runtime set
        var result = CapabilityNegotiator.Negotiate(
            ["llm.chat"],
            new HashSet<string>());

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Negotiate_LlmCompletion_IsAlwaysSatisfied()
    {
        var result = CapabilityNegotiator.Negotiate(
            ["llm.completion"],
            new HashSet<string>());

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Negotiate_InferenceAlwaysSatisfied_EvenWhenNotInRuntimeSet()
    {
        var result = CapabilityNegotiator.Negotiate(
            [CapabilityNegotiator.WellKnown.Inference],
            new HashSet<string>());

        Assert.True(result.Success);
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Negotiate_MultipleCapabilities_OneAbsent_ReportsOnlyAbsent()
    {
        var result = CapabilityNegotiator.Negotiate(
            [CapabilityNegotiator.WellKnown.NetworkOutbound, CapabilityNegotiator.WellKnown.McpTool],
            new HashSet<string>
            {
                CapabilityNegotiator.WellKnown.Inference,
                CapabilityNegotiator.WellKnown.NetworkOutbound,
                // mcp.tool NOT advertised
            });

        Assert.False(result.Success);
        Assert.Single(result.MissingRequired);
        Assert.Equal("mcp.tool", result.MissingRequired[0]);
    }
}
