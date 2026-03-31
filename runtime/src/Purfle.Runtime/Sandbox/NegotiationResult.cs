namespace Purfle.Runtime.Sandbox;

public sealed class NegotiationResult
{
    /// <summary>Capabilities absent from the runtime that caused a load failure.</summary>
    public IReadOnlyList<string> MissingRequired { get; }

    /// <summary>Always empty in the canonical model — all declared capabilities are required.</summary>
    public IReadOnlyList<string> MissingOptional { get; }

    public bool Success => MissingRequired.Count == 0;

    public NegotiationResult(
        IReadOnlyList<string> missingRequired,
        IReadOnlyList<string> missingOptional)
    {
        MissingRequired = missingRequired;
        MissingOptional = missingOptional;
    }
}
