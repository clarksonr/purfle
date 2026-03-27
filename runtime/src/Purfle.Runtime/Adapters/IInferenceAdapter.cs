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
}
