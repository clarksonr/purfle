using Purfle.Runtime.Adapters;

namespace Purfle.Runtime.OpenClaw;

/// <summary>
/// Inference adapter for OpenClaw (openai-compatible engine interface).
/// Not yet implemented — blocked on OpenClaw integration spec.
/// </summary>
public sealed class OpenClawAdapter : IInferenceAdapter
{
    public string EngineId => "openai-compatible";

    public Task<string> InvokeAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => throw new NotImplementedException("OpenClaw adapter not yet implemented.");
}
