namespace Purfle.Sdk;

/// <summary>
/// A custom tool contributed by an agent assembly.
///
/// The AIVM discovers all tools returned by <see cref="IAgent.Tools"/> at load time
/// and advertises them to the inference engine alongside the built-in sandbox tools
/// (read_file, write_file, http_get).
///
/// Tool names must be unique within an agent and must not collide with the built-in
/// names above. The AIVM enforces this at load time.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Tool name — lowercase_snake_case, unique within the agent.
    /// This becomes the function name the LLM calls.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description sent to the LLM in the tool definition.
    /// Should clearly explain what the tool does and when to use it.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema (Draft 2020-12) for the tool's input parameters.
    /// Must be a valid JSON object schema string, e.g.:
    /// <code>
    /// {"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}
    /// </code>
    /// </summary>
    string InputSchemaJson { get; }

    /// <summary>
    /// Executes the tool with the given JSON input and returns the result as plain text.
    /// The result is fed back to the LLM as a tool_result block.
    ///
    /// <para>
    /// The AIVM does not enforce sandbox permissions on agent tool arguments in v0.1.
    /// Implementations must validate paths and URLs against safe roots defensively.
    /// </para>
    ///
    /// Throw <see cref="Exception"/> on unrecoverable errors — the adapter catches and
    /// formats these as error strings returned to the model, not as exceptions to the caller.
    /// </summary>
    Task<string> ExecuteAsync(string inputJson, CancellationToken ct = default);
}
