using Purfle.Agents.WebResearch.Tools;
using Purfle.Sdk;

namespace Purfle.Agents.WebResearch;

/// <summary>
/// An agent that fetches web pages and synthesises research using built-in
/// <c>http_get</c> (provided by the adapter) and the custom <c>extract_links</c> tool.
///
/// Requires <c>network.allow</c> permissions. The manifest must deny
/// <c>https://api.anthropic.com/**</c> to prevent the agent from calling
/// the inference API directly.
/// </summary>
public sealed class WebResearchAgent : IAgent
{
    private static readonly IReadOnlyList<IAgentTool> s_tools = [new ExtractLinksTool()];

    /// <inheritdoc/>
    public string? SystemPrompt =>
        "You are a web research assistant running inside the Purfle AIVM. " +
        "You can fetch web pages using http_get and extract links using extract_links. " +
        "When a user asks you to research a topic: " +
        "1. Fetch a relevant starting URL with http_get. " +
        "2. If needed, use extract_links to find further pages to explore. " +
        "3. Synthesise the information into a clear, concise answer with source URLs. " +
        "Always cite the URL you retrieved information from. " +
        "Do not fabricate URLs — only cite pages you have actually fetched.";

    /// <inheritdoc/>
    public IReadOnlyList<IAgentTool> Tools => s_tools;
}
