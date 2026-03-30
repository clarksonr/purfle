using Purfle.Runtime.Adapters;
using Purfle.Runtime.Anthropic;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Mcp;
using Purfle.Runtime.OpenClaw;
using Purfle.Runtime.Ollama;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Host;

/// <summary>
/// Resolves the correct <see cref="IInferenceAdapter"/> for the engine declared
/// in the agent manifest. One instance is registered per host process; adapters
/// themselves may be stateless or stateful per-agent.
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
    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox)
        => manifest.Runtime.Engine switch
        {
            EngineType.Anthropic => new AnthropicAdapter(manifest, sandbox, _http, _mcpClients),
            EngineType.OpenAiCompatible => new OpenClawAdapter(),
            EngineType.Ollama => new OllamaAdapter(),
            _ => throw new NotSupportedException(
                $"No adapter registered for engine '{manifest.Runtime.Engine}'.")
        };
}
