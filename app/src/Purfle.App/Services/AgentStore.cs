using System.IO.Compression;

namespace Purfle.App.Services;

/// <summary>
/// Manages locally installed agent bundles on disk.
///
/// Layout after installation:
/// <code>
/// ~/.purfle/agents/{agentId}/
///     agent.manifest.json
///     assemblies/
///         agent.dll
///     prompts/
///         system.md   (optional)
/// </code>
/// </summary>
public sealed class AgentStore
{
    internal static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".purfle", "agents");

    internal const string ManifestFileName = "agent.manifest.json";

    public record InstalledAgent(string AgentId, string Name, string Version, string ManifestPath);

    /// <summary>
    /// Installs a <c>.purfle</c> bundle by extracting it into the local agent store.
    /// Any existing installation for <paramref name="agentId"/> is replaced.
    /// </summary>
    public string InstallBundle(string agentId, string purfleFilePath)
    {
        var dir = Path.Combine(BasePath, agentId);

        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        Directory.CreateDirectory(dir);
        ZipFile.ExtractToDirectory(purfleFilePath, dir);

        var manifestPath = Path.Combine(dir, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException(
                $"Bundle '{purfleFilePath}' does not contain '{ManifestFileName}'.");

        return manifestPath;
    }

    /// <summary>
    /// Installs a raw manifest JSON (no assembly). Used by the marketplace
    /// install flow when the agent is manifest-only.
    /// </summary>
    public string Install(string agentId, string manifestJson)
    {
        var dir  = Path.Combine(BasePath, agentId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, ManifestFileName);
        File.WriteAllText(path, manifestJson);
        return path;
    }

    /// <summary>
    /// Lists all locally installed agents.
    /// </summary>
    public List<InstalledAgent> ListInstalled()
    {
        var result = new List<InstalledAgent>();
        if (!Directory.Exists(BasePath)) return result;

        foreach (var dir in Directory.EnumerateDirectories(BasePath))
        {
            var manifestPath = Path.Combine(dir, ManifestFileName);
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root    = doc.RootElement;
                var agentId = root.GetProperty("id").GetString()   ?? Path.GetFileName(dir);
                var name    = root.GetProperty("name").GetString()    ?? "Unknown";
                var version = root.GetProperty("version").GetString() ?? "0.0.0";
                result.Add(new InstalledAgent(agentId, name, version, manifestPath));
            }
            catch
            {
                // Skip malformed manifests.
            }
        }

        return result;
    }

    /// <summary>
    /// Removes an installed agent.
    /// </summary>
    public void Uninstall(string agentId)
    {
        var dir = Path.Combine(BasePath, agentId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
