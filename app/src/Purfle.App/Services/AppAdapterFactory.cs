using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;
using AnthropicAdapter = Purfle.Runtime.Anthropic.AnthropicAdapter;
using GeminiAdapter    = Purfle.Runtime.Gemini.GeminiAdapter;

namespace Purfle.App.Services;

/// <summary>
/// Adapter factory for the MAUI app.
/// Supports engine override, API key injection, and model override.
/// </summary>
internal sealed class AppAdapterFactory(
    string? engineOverride = null,
    string? anthropicKey = null,
    string? geminiKey = null,
    string? modelOverride = null) : IAdapterFactory
{
    private readonly HttpClient _http = new();

    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox, IAgent? agent = null)
    {
        // Set API keys in environment if provided (adapters read from env)
        if (!string.IsNullOrEmpty(anthropicKey))
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", anthropicKey);
        if (!string.IsNullOrEmpty(geminiKey))
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", geminiKey);

        var engine = engineOverride ?? manifest.Runtime.Engine;
        return engine switch
        {
            "anthropic" => new AnthropicAdapter(manifest, sandbox, _http, null, agent),
            "gemini"    => new GeminiAdapter(manifest, sandbox, _http, null, agent),
            _ => throw new NotSupportedException(
                $"Engine '{manifest.Runtime.Engine}' is not supported in this runtime.")
        };
    }
}
