namespace Purfle.Runtime.Sandbox;

/// <summary>
/// Implements load sequence step 4: capability negotiation.
///
/// The agent declares what it needs; the runtime declares what it supports.
/// All declared capabilities are treated as required — the canonical manifest
/// schema has no "optional" capability concept.
///
/// "llm.chat" and "llm.completion" are implicitly satisfied by any AIVM
/// because inference is the runtime's core function.
/// </summary>
public static class CapabilityNegotiator
{
    /// <summary>
    /// Well-known capability identifiers used by the Purfle runtime.
    /// </summary>
    public static class WellKnown
    {
        /// <summary>
        /// Legacy internal inference identifier. Always implicitly satisfied.
        /// Prefer <see cref="LlmChat"/> and <see cref="LlmCompletion"/> in new code.
        /// </summary>
        public const string Inference = "inference";

        public const string LlmChat       = "llm.chat";
        public const string LlmCompletion = "llm.completion";
        public const string NetworkOutbound = "network.outbound";
        public const string EnvRead        = "env.read";
        public const string FsRead         = "fs.read";
        public const string FsWrite        = "fs.write";
        public const string McpTool        = "mcp.tool";
    }

    /// <summary>
    /// Capability IDs that are always implicitly available on any AIVM.
    /// Declaring these in the manifest is harmless but never causes a failure.
    /// </summary>
    private static readonly HashSet<string> s_alwaysSatisfied =
        new(["inference", "llm.chat", "llm.completion"], StringComparer.Ordinal);

    /// <summary>
    /// Compares <paramref name="agentCapabilities"/> against
    /// <paramref name="runtimeCapabilitySet"/> and returns a negotiation result.
    /// All declared capabilities are required in the canonical manifest model.
    /// </summary>
    public static NegotiationResult Negotiate(
        IReadOnlyList<string> agentCapabilities,
        IReadOnlySet<string> runtimeCapabilitySet)
    {
        var missing = new List<string>();

        foreach (var cap in agentCapabilities)
        {
            if (s_alwaysSatisfied.Contains(cap))
                continue;

            if (!runtimeCapabilitySet.Contains(cap))
                missing.Add(cap);
        }

        return new NegotiationResult(missing, []);
    }
}
