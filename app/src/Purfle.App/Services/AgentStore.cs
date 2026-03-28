namespace Purfle.App.Services;

/// <summary>
/// Manages locally installed agent manifests on disk.
/// </summary>
public sealed class AgentStore
{
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".purfle", "agents");

    public record InstalledAgent(string AgentId, string Name, string Version, string ManifestPath);

    /// <summary>
    /// Saves a manifest to the local store.
    /// </summary>
    public string Install(string agentId, string manifestJson)
    {
        var dir = Path.Combine(BasePath, agentId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "agent.json");
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
            var manifestPath = Path.Combine(dir, "agent.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var agentId = root.GetProperty("id").GetString() ?? Path.GetFileName(dir);
                var name = root.GetProperty("name").GetString() ?? "Unknown";
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
