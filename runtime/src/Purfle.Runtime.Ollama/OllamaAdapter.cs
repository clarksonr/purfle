using Purfle.Runtime.Adapters;

namespace Purfle.Runtime.Ollama;

/// <summary>
/// Inference adapter for Ollama (local / edge device inference).
/// Phase 4 implementation — not yet built.
/// </summary>
public sealed class OllamaAdapter : IInferenceAdapter
{
    public string EngineId => "ollama";

    public Task<string> InvokeAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => throw new NotImplementedException("Ollama adapter not yet implemented.");
}
