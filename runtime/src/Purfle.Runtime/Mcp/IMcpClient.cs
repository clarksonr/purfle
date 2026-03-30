namespace Purfle.Runtime.Mcp;

/// <summary>
/// Abstraction over a connection to an MCP (Model Context Protocol) server.
/// Each MCP server exposes a set of tools that an agent may invoke, subject
/// to sandbox enforcement via <c>permissions.tools.mcp</c>.
/// </summary>
public interface IMcpClient : IAsyncDisposable
{
    /// <summary>
    /// Lists the tools available on this MCP server. Each entry contains the
    /// tool name, description, and JSON Schema for the input parameters.
    /// </summary>
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default);

    /// <summary>
    /// Invokes a tool on the MCP server and returns the result as a string.
    /// </summary>
    /// <param name="toolName">The name of the tool to invoke.</param>
    /// <param name="arguments">JSON-serialized arguments matching the tool's input schema.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tool's text result.</returns>
    Task<string> CallToolAsync(string toolName, string arguments, CancellationToken ct = default);
}

/// <summary>
/// Describes a tool exposed by an MCP server.
/// </summary>
/// <param name="Name">Tool identifier (matches <c>permissions.tools.mcp</c> entries).</param>
/// <param name="Description">Human-readable description for the model.</param>
/// <param name="InputSchemaJson">JSON Schema (as a JSON string) describing the tool's parameters.</param>
public sealed record McpToolInfo(string Name, string Description, string InputSchemaJson);
