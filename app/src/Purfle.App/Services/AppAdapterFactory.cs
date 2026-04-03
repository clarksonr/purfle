using Purfle.Runtime.Adapters;
using Purfle.Runtime.Auth;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;
using AnthropicAdapter = Purfle.Runtime.Anthropic.AnthropicAdapter;
using GeminiAdapter    = Purfle.Runtime.Gemini.GeminiAdapter;

namespace Purfle.App.Services;

/// <summary>
/// Adapter factory for the MAUI app.
/// Supports engine override, API key injection, model override, and resolved credentials.
/// </summary>
internal sealed class AppAdapterFactory(
    string? engineOverride = null,
    string? anthropicKey = null,
    string? geminiKey = null,
    string? modelOverride = null,
    ResolvedCredential? resolvedCredential = null) : IAdapterFactory
{
    private readonly HttpClient _http = new();

    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox, IAgent? agent = null)
    {
        // Legacy path: set API keys in environment if provided (adapters read from env)
        if (!string.IsNullOrEmpty(anthropicKey))
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", anthropicKey);
        if (!string.IsNullOrEmpty(geminiKey))
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", geminiKey);

        var engine = resolvedCredential?.Provider ?? engineOverride ?? manifest.Runtime.Engine;
        return engine switch
        {
            "anthropic" => new AnthropicAdapter(manifest, sandbox, _http, null, agent, resolvedCredential),
            "gemini"    => new GeminiAdapter(manifest, sandbox, _http, null, agent, resolvedCredential),
            _ => throw new NotSupportedException(
                $"Engine '{engine}' is not supported in this runtime.")
        };
    }
}
