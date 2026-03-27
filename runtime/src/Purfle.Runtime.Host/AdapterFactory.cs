using Purfle.Runtime.Adapters;
using Purfle.Runtime.Anthropic;
using Purfle.Runtime.Manifest;
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

    public AdapterFactory(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    /// <inheritdoc/>
    public IInferenceAdapter Create(AgentManifest manifest, AgentSandbox sandbox)
        => manifest.Runtime.Engine switch
        {
            EngineType.Anthropic => new AnthropicAdapter(manifest, sandbox, _http),
            EngineType.OpenAiCompatible => new OpenClawAdapter(),
            EngineType.Ollama => new OllamaAdapter(),
            _ => throw new NotSupportedException(
                $"No adapter registered for engine '{manifest.Runtime.Engine}'.")
        };
}
