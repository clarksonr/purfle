using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Adapters;

/// <summary>
/// Resolves an <see cref="IInferenceAdapter"/> from the manifest's
/// <c>runtime.engine</c> field. Implemented in the host or composition root,
/// not in Purfle.Runtime, to avoid circular project references.
/// </summary>
public interface IAdapterFactory
{
    /// <summary>
    /// Creates the adapter appropriate for the manifest's declared engine.
    /// </summary>
    /// <param name="manifest">The fully loaded agent manifest.</param>
    /// <param name="sandbox">The agent's permission sandbox.</param>
    /// <returns>A ready-to-use inference adapter.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when no adapter is registered for the manifest's engine type.
    /// </exception>
    IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox);
}
