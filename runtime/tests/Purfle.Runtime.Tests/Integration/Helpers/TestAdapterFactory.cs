using Purfle.Runtime.Adapters;
using Purfle.Runtime.Anthropic;
using AnthropicAdapter = Purfle.Runtime.Anthropic.AnthropicAdapter;
using Purfle.Runtime.Gemini;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;

namespace Purfle.Runtime.Tests.Integration.Helpers;

/// <summary>
/// Adapter factory for live AI integration tests.
/// Resolves the correct adapter based on the engine declared in the manifest,
/// exactly as the production <c>AdapterFactory</c> in the host does.
/// </summary>
internal sealed class TestAdapterFactory : IAdapterFactory
{
    private readonly HttpClient _http = new();

    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox, IAgent? agent = null)
        => manifest.Runtime.Engine switch
        {
            "anthropic"          => new AnthropicAdapter(manifest, sandbox, _http, null, agent),
            "gemini"             => new GeminiAdapter(manifest, sandbox, _http, null, agent),
            _ => throw new NotSupportedException(
                $"Engine '{manifest.Runtime.Engine}' is not wired in TestAdapterFactory.")
        };
}
