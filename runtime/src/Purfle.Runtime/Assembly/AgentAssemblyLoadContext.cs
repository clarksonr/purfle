using System.Reflection;
using System.Runtime.Loader;

namespace Purfle.Runtime.Assembly;

/// <summary>
/// An isolated <see cref="AssemblyLoadContext"/> for a single agent.
///
/// Each loaded agent gets its own context, which provides:
/// <list type="bullet">
///   <item>Type isolation — no conflicts between agents that ship different versions of the same dependency.</item>
///   <item>Clean unload — the GC can collect all agent types once <see cref="AssemblyLoadContext.Unload"/> is called.</item>
/// </list>
///
/// <para>
/// <strong>Critical:</strong> <c>Purfle.Sdk.dll</c> must NOT be present in the agent's
/// <c>assemblies/</c> directory. When the CLR resolves <c>Purfle.Sdk</c> for the agent
/// assembly, this context returns <c>null</c>, causing the CLR to fall through to the
/// default ALC where the runtime already holds it. This keeps <c>IAgent</c> type identity
/// coherent — both the runtime and the agent assembly see the same <c>IAgent</c> type.
/// </para>
/// </summary>
internal sealed class AgentAssemblyLoadContext : AssemblyLoadContext
{
    private static readonly string SdkAssemblyName = "Purfle.Sdk";

    private readonly string _assembliesDirectory;

    public AgentAssemblyLoadContext(string assembliesDirectory)
        : base(isCollectible: true)
    {
        _assembliesDirectory = assembliesDirectory;
    }

    /// <inheritdoc/>
    protected override System.Reflection.Assembly? Load(AssemblyName assemblyName)
    {
        // Never load Purfle.Sdk from the agent directory — it must come from the
        // default ALC so that IAgent type identity is shared with the runtime.
        if (string.Equals(assemblyName.Name, SdkAssemblyName, StringComparison.OrdinalIgnoreCase))
            return null;

        // Probe the agent's assemblies directory first.
        var candidate = Path.Combine(_assembliesDirectory, (assemblyName.Name ?? "") + ".dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

        // Fall through to the default ALC for everything else (BCL, shared framework).
        return null;
    }
}
