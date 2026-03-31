using Purfle.Runtime.Adapters;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;
using AnthropicAdapter = Purfle.Runtime.Anthropic.AnthropicAdapter;
using GeminiAdapter    = Purfle.Runtime.Gemini.GeminiAdapter;

namespace Purfle.App.Services;

/// <summary>
/// Adapter factory for the MAUI app.
/// If <paramref name="engineOverride"/> is set, it takes precedence over the engine
/// declared in the manifest — the manifest value is treated as a hint only.
/// </summary>
internal sealed class AppAdapterFactory(string? engineOverride = null) : IAdapterFactory
{
    private readonly HttpClient _http = new();

    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox, IAgent? agent = null)
        => (engineOverride ?? manifest.Runtime.Engine) switch
        {
            "anthropic" => new AnthropicAdapter(manifest, sandbox, _http, null, agent),
            "gemini"    => new GeminiAdapter(manifest, sandbox, _http, null, agent),
            _ => throw new NotSupportedException(
                $"Engine '{manifest.Runtime.Engine}' is not supported in this runtime.")
        };
}
