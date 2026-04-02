using System.Text;
using Purfle.Runtime;
using Purfle.Runtime.Adapters;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Lifecycle;
using Purfle.Runtime.Manifest;
using Purfle.Runtime.Sandbox;

namespace Purfle.App.Services;

/// <summary>
/// Loads and prepares installed agents for execution.
///
/// Uses the live key registry (<see cref="IKeyRegistry"/>) to verify agent
/// signatures via the full 7-step load sequence. Agents must be signed with a
/// key registered in the registry — ephemeral local-dev re-signing is no longer
/// used in production code paths.
/// </summary>
public sealed class AgentExecutorService(AgentStore store, IKeyRegistry registry, CredentialService credentials)
{
    private const string FallbackSystemPrompt =
        "You are a helpful agent running inside the Purfle AIVM. Be concise.";

    private static readonly IReadOnlySet<string> RuntimeCapabilities = new HashSet<string>
    {
        CapabilityNegotiator.WellKnown.Inference,
        CapabilityNegotiator.WellKnown.LlmChat,
        CapabilityNegotiator.WellKnown.LlmCompletion,
        CapabilityNegotiator.WellKnown.NetworkOutbound,
        CapabilityNegotiator.WellKnown.EnvRead,
        CapabilityNegotiator.WellKnown.FsRead,
        CapabilityNegotiator.WellKnown.FsWrite,
        CapabilityNegotiator.WellKnown.McpTool,
    };

    /// <summary>
    /// Loads the agent with the given <paramref name="agentId"/> from the local store.
    ///
    /// Returns the inference adapter, the agent's display name, the system prompt to
    /// use (from the agent assembly if present, otherwise a default), and any error message.
    /// </summary>
    public async Task<(IInferenceAdapter? Adapter, string AgentName, string Description, string SystemPrompt, string? Error)>
        LoadAsync(string agentId, CancellationToken ct = default)
    {
        var anthropicKey = await credentials.GetAnthropicKeyAsync();
        var geminiKey    = await credentials.GetGeminiKeyAsync();

        var installed = store.ListInstalled().FirstOrDefault(a => a.AgentId == agentId);
        if (installed is null)
            return (null, agentId, "", FallbackSystemPrompt, $"Agent '{agentId}' is not installed.");

        string manifestJson;
        try
        {
            manifestJson = await File.ReadAllTextAsync(installed.ManifestPath, ct);
        }
        catch (Exception ex)
        {
            return (null, installed.Name, "", FallbackSystemPrompt, $"Could not read manifest: {ex.Message}");
        }

        // Detect assemblies directory.
        var agentDir      = Path.GetDirectoryName(installed.ManifestPath)!;
        var assembliesDir = Path.Combine(agentDir, "assemblies");
        var hasAssemblies = Directory.Exists(assembliesDir) &&
                            File.Exists(Path.Combine(assembliesDir, "agent.dll"));

        var engineOverride = Preferences.Get("preferred_engine", "") switch
        {
            "anthropic" => "anthropic",
            "gemini"    => "gemini",
            _           => (string?)null,
        };

        var modelOverride = Preferences.Get("preferred_gemini_model", "") is { Length: > 0 } m ? m : null;

        var loader = new AgentLoader(
            identityVerifier:    new IdentityVerifier(registry),
            runtimeCapabilities: RuntimeCapabilities,
            adapterFactory:      new AppAdapterFactory(engineOverride, anthropicKey, geminiKey, modelOverride));

        var result = await loader.LoadAsync(
            manifestJson,
            assembliesDirectory: hasAssemblies ? assembliesDir : null,
            ct: ct);

        if (!result.Success)
            return (null, installed.Name, "", FallbackSystemPrompt,
                $"Load failed [{result.FailureReason}]: {result.FailureMessage}");

        var manifest = result.Manifest!;
        var systemPrompt = result.AgentInstance?.SystemPrompt
                           ?? BuildSystemPrompt(manifest);
        return (result.Adapter, installed.Name, manifest.Description ?? "", systemPrompt, null);
    }

    /// <summary>
    /// Builds a system prompt from the manifest when no agent assembly (and its
    /// <see cref="Purfle.Sdk.IAgent.SystemPrompt"/>) is available.
    /// </summary>
    private static string BuildSystemPrompt(AgentManifest manifest)
    {
        var sb = new StringBuilder();
        sb.Append($"You are {manifest.Name}. {manifest.Description}");

        if (manifest.Capabilities.Contains("fs.read"))
            sb.Append(" You have access to three file tools: " +
                      "(1) find_files — locate files by name or pattern, e.g. 'CLAUDE.md' or '*.json'. Use this when the user asks to find a file by name. " +
                      "(2) search_files — search the text contents of files for a keyword or phrase. Use this when the user asks what is inside files or to search for a word or phrase. " +
                      "(3) read_file — read the full contents of a specific file path. " +
                      "Always call a tool rather than saying you cannot access files.");
        if (manifest.Capabilities.Contains("network.outbound"))
            sb.Append(" You can fetch URLs using the http_get tool.");

        sb.Append(" Be concise and always use your tools when they are relevant.");
        return sb.ToString();
    }


}
