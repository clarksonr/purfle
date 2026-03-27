using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Sandbox;

public sealed class NegotiationResult
{
    /// <summary>Required capabilities absent from the runtime. Non-empty = load failure.</summary>
    public IReadOnlyList<AgentCapability> MissingRequired { get; }

    /// <summary>Optional capabilities absent from the runtime. Agent must degrade gracefully.</summary>
    public IReadOnlyList<AgentCapability> MissingOptional { get; }

    public bool Success => MissingRequired.Count == 0;

    public NegotiationResult(
        IReadOnlyList<AgentCapability> missingRequired,
        IReadOnlyList<AgentCapability> missingOptional)
    {
        MissingRequired = missingRequired;
        MissingOptional = missingOptional;
    }
}
