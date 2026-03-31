using Purfle.Sdk;

namespace Purfle.Agents.Chat;

/// <summary>
/// A general-purpose conversational agent with no filesystem or network access.
/// This is the simplest possible agent — it demonstrates the load pipeline
/// without any custom tool complexity.
/// </summary>
public sealed class ChatAgent : IAgent
{
    /// <inheritdoc/>
    public string? SystemPrompt =>
        "You are a helpful, friendly conversational assistant running inside the Purfle AIVM. " +
        "Respond concisely and clearly. " +
        "You do not have access to external files, the internet, or any other external systems. " +
        "If asked to do something outside your capabilities, explain politely what you cannot do.";

    /// <inheritdoc/>
    public IReadOnlyList<IAgentTool> Tools => [];
}
