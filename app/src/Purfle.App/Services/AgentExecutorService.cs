using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Uses the "local dev" trust model: the manifest on disk is trusted as-is.
/// An ephemeral P-256 key is generated at load time, the manifest is re-signed
/// with that key, and the key is registered in a <see cref="StaticKeyRegistry"/>.
/// This lets the full 7-step load sequence run without requiring the original
/// publisher key to be present locally.
/// </summary>
public sealed class AgentExecutorService(AgentStore store)
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
    public async Task<(IInferenceAdapter? Adapter, string AgentName, string SystemPrompt, string? Error)>
        LoadAsync(string agentId, CancellationToken ct = default)
    {
        // Inject API keys from SecureStorage if not already set in the environment.
        await InjectKeyAsync("ANTHROPIC_API_KEY", "anthropic_api_key");
        await InjectKeyAsync("GEMINI_API_KEY",    "gemini_api_key");

        var installed = store.ListInstalled().FirstOrDefault(a => a.AgentId == agentId);
        if (installed is null)
            return (null, agentId, FallbackSystemPrompt, $"Agent '{agentId}' is not installed.");

        string manifestJson;
        try
        {
            manifestJson = await File.ReadAllTextAsync(installed.ManifestPath, ct);
        }
        catch (Exception ex)
        {
            return (null, installed.Name, FallbackSystemPrompt, $"Could not read manifest: {ex.Message}");
        }

        // Generate an ephemeral key pair and re-sign the manifest so the full
        // load sequence passes without needing the original publisher key.
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecKey.ExportParameters(includePrivateParameters: false);
        var ephemeralKey = new PublicKey
        {
            KeyId     = "local-dev-key",
            Algorithm = "ES256",
            X         = p.Q.X!,
            Y         = p.Q.Y!,
        };

        string signedJson;
        try
        {
            signedJson = ResignManifest(manifestJson, ecKey, ephemeralKey.KeyId);
        }
        catch (Exception ex)
        {
            return (null, installed.Name, FallbackSystemPrompt, $"Could not re-sign manifest: {ex.Message}");
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

        var registry = new StaticKeyRegistry([ephemeralKey]);
        var loader   = new AgentLoader(
            identityVerifier:    new IdentityVerifier(registry),
            runtimeCapabilities: RuntimeCapabilities,
            adapterFactory:      new AppAdapterFactory(engineOverride));

        var result = await loader.LoadAsync(
            signedJson,
            assembliesDirectory: hasAssemblies ? assembliesDir : null,
            ct: ct);

        if (!result.Success)
            return (null, installed.Name, FallbackSystemPrompt,
                $"Load failed [{result.FailureReason}]: {result.FailureMessage}");

        var systemPrompt = result.AgentInstance?.SystemPrompt
                           ?? BuildSystemPrompt(result.Manifest!);
        return (result.Adapter, installed.Name, systemPrompt, null);
    }

    /// <summary>
    /// Builds a system prompt from the manifest when no agent assembly (and its
    /// <see cref="Purfle.Sdk.IAgent.SystemPrompt"/>) is available. Mentions the
    /// tools that the adapter will offer so the model knows to call them.
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

    private static async Task InjectKeyAsync(string envVar, string storageKey)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar))) return;
        var stored = await SecureStorage.GetAsync(storageKey);
        if (!string.IsNullOrEmpty(stored))
            Environment.SetEnvironmentVariable(envVar, stored);
    }

    // ── Re-signing helpers (mirrors runtime/src/Purfle.Runtime.Host/Program.cs) ──

    private static string ResignManifest(string manifestJson, ECDsa key, string keyId)
    {
        var dict     = Deserialize(manifestJson);
        var identity = ToDict(dict["identity"]);
        identity["key_id"]    = Str(keyId);
        identity["signature"] = Str("placeholder");
        dict["identity"]      = Obj(identity);

        var withPlaceholder = JsonSerializer.Serialize(dict);
        var sig             = ComputeJws(withPlaceholder, key, keyId);

        identity["signature"] = Str(sig);
        dict["identity"]      = Obj(identity);
        return JsonSerializer.Serialize(dict);
    }

    private static string ComputeJws(string manifestJson, ECDsa key, string keyId)
    {
        var canonical  = CanonicalJson.ForSigning(manifestJson);
        var header     = $$$"""{"alg":"ES256","kid":"{{{keyId}}}"}""";
        var headerB64  = B64(Encoding.UTF8.GetBytes(header));
        var payloadB64 = B64(canonical);
        var input      = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var sig        = key.SignData(input, HashAlgorithmName.SHA256,
                             DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{headerB64}.{payloadB64}.{B64(sig)}";
    }

    private static string B64(byte[] b) =>
        Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static Dictionary<string, JsonElement> Deserialize(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static Dictionary<string, JsonElement> ToDict(JsonElement el) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(el.GetRawText())!;

    private static JsonElement Str(string s) =>
        JsonDocument.Parse(JsonSerializer.Serialize(s)).RootElement;

    private static JsonElement Obj(Dictionary<string, JsonElement> d) =>
        JsonDocument.Parse(JsonSerializer.Serialize(d)).RootElement;
}
