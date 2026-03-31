using System.Runtime.Loader;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;

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

    /// <summary>
    /// The instantiated agent entry point from the agent assembly. Non-null when
    /// the agent was loaded from a bundle containing an <c>assemblies/agent.dll</c>.
    /// </summary>
    public IAgent? AgentInstance { get; private init; }

    /// <summary>
    /// The <see cref="AssemblyLoadContext"/> that owns the agent assembly.
    /// Call <see cref="AssemblyLoadContext.Unload"/> to release the agent's types.
    /// Null when no assembly was loaded.
    /// </summary>
    public AssemblyLoadContext? LoadContext { get; private init; }

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
        IInferenceAdapter? adapter = null,
        IAgent? agentInstance = null,
        AssemblyLoadContext? loadContext = null) => new()
    {
        Success       = true,
        Manifest      = manifest,
        Sandbox       = sandbox,
        Adapter       = adapter,
        AgentInstance = agentInstance,
        LoadContext   = loadContext,
        Warnings      = warnings,
    };

    public static LoadResult Fail(
        LoadFailureReason reason,
        string message,
        IReadOnlyList<string>? warnings = null) => new()
    {
        Success        = false,
        FailureReason  = reason,
        FailureMessage = message,
        Warnings       = warnings ?? [],
    };
}
