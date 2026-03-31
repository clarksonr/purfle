namespace Purfle.Sdk;

/// <summary>
/// Entry point for a Purfle agent assembly.
///
/// The AIVM loads the agent's DLL into an isolated <c>AssemblyLoadContext</c> and
/// scans it for exactly one exported non-abstract type implementing this interface.
/// The type must have a public parameterless constructor.
///
/// <para>
/// Agent assemblies must NOT ship <c>Purfle.Sdk.dll</c> inside the bundle's
/// <c>assemblies/</c> folder. The AIVM resolves <c>Purfle.Sdk</c> from its own
/// default ALC, ensuring type identity is consistent across the ALC boundary.
/// </para>
/// </summary>
public interface IAgent
{
    /// <summary>
    /// System prompt injected at every invocation.
    ///
    /// When non-null this takes precedence over <c>prompts/system.md</c> in the package.
    /// When null the AIVM falls back to <c>prompts/system.md</c> if present, then to a
    /// generic default.
    /// </summary>
    string? SystemPrompt { get; }

    /// <summary>
    /// Custom tools contributed by this agent. May be empty but must not be null.
    ///
    /// Tools are discovered once at load time and registered with the inference adapter.
    /// Tool names must be lowercase_snake_case and must not collide with built-in tool
    /// names (<c>read_file</c>, <c>write_file</c>, <c>http_get</c>).
    /// </summary>
    IReadOnlyList<IAgentTool> Tools { get; }
}
