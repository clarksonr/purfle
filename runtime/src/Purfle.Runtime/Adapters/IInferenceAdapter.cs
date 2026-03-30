namespace Purfle.Runtime.Adapters;

/// <summary>
/// Abstraction over an inference engine. Each engine implementation (Anthropic,
/// OpenClaw, Ollama) provides one adapter. The adapter is resolved by the
/// IAdapterFactory during load sequence step 7.
/// </summary>
public interface IInferenceAdapter
{
    /// <summary>Engine identifier matching <c>runtime.engine</c> in the manifest.</summary>
    string EngineId { get; }

    /// <summary>
    /// Sends a single-turn inference request to the engine.
    /// </summary>
    /// <param name="systemPrompt">System-level instructions for the agent.</param>
    /// <param name="userMessage">The user's input message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The model's text response.</returns>
    Task<string> InvokeAsync(string systemPrompt, string userMessage, CancellationToken ct = default);

    /// <summary>
    /// Sends a multi-turn inference request with conversation history.
    /// Returns the model's text reply and the updated message list including the
    /// new user and assistant turns, so the caller can feed it back for subsequent turns.
    /// </summary>
    /// <param name="systemPrompt">System-level instructions for the agent.</param>
    /// <param name="conversationHistory">Prior message objects from previous turns.</param>
    /// <param name="userMessage">The new user message for this turn.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of the model's text reply and the updated conversation history.</returns>
    Task<(string Reply, List<object> UpdatedHistory)> InvokeMultiTurnAsync(
        string systemPrompt,
        List<object> conversationHistory,
        string userMessage,
        CancellationToken ct = default);
}
