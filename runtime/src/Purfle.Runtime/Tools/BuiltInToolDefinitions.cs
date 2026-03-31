using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Tools;

/// <summary>
/// Neutral tool metadata for the built-in tools. Adapters call <see cref="For"/> to get
/// the applicable specs for a given permission set, then translate to their own wire
/// format (Anthropic <c>tool_use</c>, Gemini <c>functionDeclarations</c>, etc.).
/// </summary>
public static class BuiltInToolDefinitions
{
    /// <summary>
    /// Returns the built-in tool specs applicable for the given permissions.
    /// No tools are included for permission blocks that are absent or empty.
    /// </summary>
    public static IReadOnlyList<BuiltInToolSpec> For(AgentPermissions permissions)
    {
        var specs = new List<BuiltInToolSpec>();

        if (permissions.Filesystem?.Read.Count > 0)
        {
            specs.Add(new BuiltInToolSpec(
                Name: "find_files",
                Description: "Find files by name in the user's Downloads directory. " +
                             "Use this when the user wants to locate a file by name rather than search its contents.",
                Parameters:
                [
                    new("name_pattern", "string",
                        "Filename pattern to match, e.g. 'CLAUDE.md', '*.json'. Defaults to all files."),
                ],
                Required: []));

            specs.Add(new BuiltInToolSpec(
                Name: "search_files",
                Description: "Search for text within files in the user's Downloads directory. " +
                             "Returns matching lines with file path and line number.",
                Parameters:
                [
                    new("query",        "string", "Text to search for (case-insensitive)."),
                    new("file_pattern", "string", "Optional filename glob pattern, e.g. '*.cs', '*.txt'. Defaults to all files."),
                ],
                Required: ["query"]));

            specs.Add(new BuiltInToolSpec(
                Name: "read_file",
                Description: "Read the full text contents of a file on the local filesystem. " +
                             "Only paths matching the agent's filesystem.read permission patterns are accessible.",
                Parameters:
                [
                    new("path", "string", "Absolute path to the file to read."),
                ],
                Required: ["path"]));
        }

        if (permissions.Filesystem?.Write.Count > 0)
        {
            specs.Add(new BuiltInToolSpec(
                Name: "write_file",
                Description: "Write text content to a file on the local filesystem. " +
                             "Only paths matching the agent's filesystem.write permission patterns are accessible.",
                Parameters:
                [
                    new("path",    "string", "Absolute path to the file to write."),
                    new("content", "string", "Text content to write to the file."),
                ],
                Required: ["path", "content"]));
        }

        if (permissions.Network?.Allow.Count > 0)
        {
            specs.Add(new BuiltInToolSpec(
                Name: "http_get",
                Description: "Fetch a URL via HTTP GET and return the response body as text. " +
                             "Only URLs matching the agent's network.allow patterns are accessible.",
                Parameters:
                [
                    new("url", "string", "The URL to fetch."),
                ],
                Required: ["url"]));
        }

        return specs;
    }
}

/// <summary>Adapter-neutral description of a built-in tool.</summary>
public sealed record BuiltInToolSpec(
    string Name,
    string Description,
    IReadOnlyList<BuiltInParamSpec> Parameters,
    IReadOnlyList<string> Required);

/// <summary>A single parameter within a <see cref="BuiltInToolSpec"/>.</summary>
public sealed record BuiltInParamSpec(string Name, string Type, string Description);
