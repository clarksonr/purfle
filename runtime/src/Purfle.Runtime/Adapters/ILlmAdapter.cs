namespace Purfle.Runtime.Adapters;

/// <summary>
/// Simplified inference interface used by scheduled agent runners.
/// Wraps a single-turn request: system prompt + user message → text response.
/// </summary>
public interface ILlmAdapter
{
    /// <summary>
    /// Sends a single-turn request to the LLM and returns the text response.
    /// </summary>
    /// <param name="systemPrompt">System-level instructions for the agent.</param>
    /// <param name="userMessage">The user message for this turn.</param>
    Task<string> CompleteAsync(string systemPrompt, string userMessage);
}
