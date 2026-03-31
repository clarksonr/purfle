using Purfle.Agents.FileSearch.Tools;
using Purfle.Sdk;

namespace Purfle.Agents.FileSearch;

/// <summary>
/// An agent that searches file contents using a grep-like <c>search_files</c> tool.
/// Requires <c>filesystem.read</c> permissions to cover the directories being searched.
/// </summary>
public sealed class FileSearchAgent : IAgent
{
    private static readonly IReadOnlyList<IAgentTool> s_tools = [new FileSearchTool()];

    /// <inheritdoc/>
    public string? SystemPrompt =>
        "You are a file search assistant running inside the Purfle AIVM. " +
        "You can search the contents of files in directories for specific text using the search_files tool. " +
        "When a user asks you to find something in their files, use search_files with a relevant query. " +
        "Always report the file path and line number when you find a match. " +
        "If you find many results, summarise the most relevant ones. " +
        "You can only read files — you cannot write, delete, or move them.";

    /// <inheritdoc/>
    public IReadOnlyList<IAgentTool> Tools => s_tools;
}
