using Purfle.Runtime.Adapters;
using Purfle.Runtime.Anthropic;
using Purfle.Runtime.Gemini;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.OpenClaw;
using Purfle.Runtime.Ollama;
using Purfle.Runtime.Sandbox;
using Purfle.Sdk;

namespace Purfle.Runtime.Host;

/// <summary>
/// Resolves the correct <see cref="IInferenceAdapter"/> for the engine declared
/// in the agent manifest. One instance is registered per host process.
/// </summary>
public sealed class AdapterFactory : IAdapterFactory
{
    private readonly HttpClient _http;
    private readonly IReadOnlyList<IMcpClient>? _mcpClients;

    public AdapterFactory(HttpClient? http = null, IReadOnlyList<IMcpClient>? mcpClients = null)
    {
        _http = http ?? new HttpClient();
        _mcpClients = mcpClients;
    }

    /// <inheritdoc/>
    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox, IAgent? agent = null)
        => manifest.Runtime.Engine switch
        {
            "anthropic"        => new AnthropicAdapter(manifest, sandbox, _http, _mcpClients, agent),
            "gemini"           => new GeminiAdapter(manifest, sandbox, _http, _mcpClients, agent),
            "openai-compatible" => new OpenClawAdapter(),
            "ollama"           => new OllamaAdapter(),
            _ => throw new NotSupportedException(
                $"No adapter registered for engine '{manifest.Runtime.Engine}'.")
        };
}
