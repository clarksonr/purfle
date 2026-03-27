using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Lifecycle;

public sealed class LoadResult
{
    public bool Success { get; private init; }
    public AgentManifest? Manifest { get; private init; }
    public AgentSandbox? Sandbox { get; private init; }

    /// <summary>
    /// The resolved inference adapter for this agent. Non-null on success when
    /// an <see cref="IAdapterFactory"/> was provided to <see cref="AgentLoader"/>.
    /// </summary>
    public IInferenceAdapter? Adapter { get; private init; }

    public LoadFailureReason? FailureReason { get; private init; }
    public string? FailureMessage { get; private init; }

    /// <summary>
    /// Warnings emitted during load (e.g. optional capabilities absent from runtime).
    /// Present even on success.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    public static LoadResult Ok(
        AgentManifest manifest,
        AgentSandbox sandbox,
        IReadOnlyList<string> warnings,
        IInferenceAdapter? adapter = null) => new()
    {
        Success = true,
        Manifest = manifest,
        Sandbox = sandbox,
        Adapter = adapter,
        Warnings = warnings,
    };

    public static LoadResult Fail(LoadFailureReason reason, string message, IReadOnlyList<string>? warnings = null) => new()
    {
        Success = false,
        FailureReason = reason,
        FailureMessage = message,
        Warnings = warnings ?? [],
    };
}
