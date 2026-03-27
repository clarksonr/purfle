using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Sandbox;

/// <summary>
/// Implements load sequence step 4: capability negotiation.
///
/// The agent declares what it requires; the runtime declares what it offers.
/// Any required capability absent from the runtime's set is a load failure.
/// Missing optional capabilities produce warnings only.
/// </summary>
public static class CapabilityNegotiator
{
    /// <summary>
    /// Well-known capability IDs reserved by the Purfle registry.
    /// Runtimes may support any subset of these plus third-party namespaced IDs.
    /// </summary>
    public static class WellKnown
    {
        /// <summary>
        /// Always implicitly required. Declaring it in the manifest has no effect.
        /// Listed here so runtimes can include it in their advertised set.
        /// </summary>
        public const string Inference = "inference";

        public const string WebSearch       = "web-search";
        public const string Filesystem      = "filesystem";
        public const string McpTools        = "mcp-tools";
        public const string CodeExecution   = "code-execution";
        public const string TextToSpeech    = "text-to-speech";
        public const string SpeechToText    = "speech-to-text";
    }

    /// <summary>
    /// Compares <paramref name="agentCapabilities"/> against
    /// <paramref name="runtimeCapabilitySet"/> and returns a negotiation result.
    /// </summary>
    public static NegotiationResult Negotiate(
        IReadOnlyList<AgentCapability> agentCapabilities,
        IReadOnlySet<string> runtimeCapabilitySet)
    {
        var missingRequired = new List<AgentCapability>();
        var missingOptional = new List<AgentCapability>();

        foreach (var cap in agentCapabilities)
        {
            // "inference" is always implicitly satisfied; skip it.
            if (cap.Id.Equals(WellKnown.Inference, StringComparison.Ordinal))
                continue;

            if (!runtimeCapabilitySet.Contains(cap.Id))
            {
                if (cap.Required)
                    missingRequired.Add(cap);
                else
                    missingOptional.Add(cap);
            }
        }

        return new NegotiationResult(missingRequired, missingOptional);
    }
}
