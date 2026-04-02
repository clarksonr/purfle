using System.Text.Json;
using System.Text.RegularExpressions;
using Purfle.Runtime.Manifest;

namespace Purfle.Runtime.Sandbox;

/// <summary>
/// The enforced resource boundary for a loaded agent, derived from the manifest
/// permissions block. Created during load sequence step 5. Immutable for the
/// lifetime of the agent.
/// </summary>
public sealed class AgentSandbox
{
    private readonly AgentPermissions _permissions;

    /// <summary>
    /// Constructs a sandbox from a typed <see cref="AgentPermissions"/> object.
    /// Used directly in unit tests; adapters use <see cref="GetPermissions"/> to
    /// build tool lists.
    /// </summary>
    public AgentSandbox(AgentPermissions permissions)
    {
        _permissions = permissions;
    }

    /// <summary>
    /// Constructs a sandbox from the canonical manifest permissions block
    /// (<c>Dictionary&lt;string, JsonElement&gt;?</c>).
    /// Keys are capability strings ("network.outbound", "env.read", etc.).
    /// </summary>
    public AgentSandbox(Dictionary<string, JsonElement>? canonicalPermissions)
    {
        _permissions = ParseCanonical(canonicalPermissions);
    }

    /// <summary>
    /// Returns the internal typed permissions, used by adapters to determine
    /// which built-in tools to advertise.
    /// </summary>
    public AgentPermissions GetPermissions() => _permissions;

    // ── Public enforcement API ────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the agent may make an outbound request to <paramref name="url"/>.
    /// </summary>
    public bool CanAccessUrl(string url)
    {
        var net = _permissions.Network;
        if (net is null) return false;

        if (net.Deny.Any(pattern => UrlPatternMatch(pattern, url)))
            return false;

        return net.Allow.Any(pattern => UrlPatternMatch(pattern, url));
    }

    /// <summary>Returns true if the agent may read <paramref name="path"/>.</summary>
    public bool CanReadPath(string path)
    {
        var fs = _permissions.Filesystem;
        if (fs is null) return false;
        return fs.Read.Any(pattern => GlobMatch(pattern, path));
    }

    /// <summary>Returns true if the agent may write to <paramref name="path"/>.</summary>
    public bool CanWritePath(string path)
    {
        var fs = _permissions.Filesystem;
        if (fs is null) return false;
        return fs.Write.Any(pattern => GlobMatch(pattern, path));
    }

    /// <summary>Returns true if the agent may read environment variable <paramref name="name"/>.</summary>
    public bool CanReadEnv(string name)
    {
        var env = _permissions.Environment;
        if (env is null) return false;
        return env.Allow.Contains(name, StringComparer.Ordinal);
    }

    /// <summary>Returns a human-readable summary of allowed write paths.</summary>
    public string GetWritePathsSummary()
    {
        var fs = _permissions.Filesystem;
        if (fs is null || fs.Write.Count == 0) return "(none)";
        return string.Join(", ", fs.Write);
    }

    /// <summary>Returns true if the agent may invoke MCP tool <paramref name="toolId"/>.</summary>
    public bool CanUseMcpTool(string toolId)
    {
        var tools = _permissions.Tools;
        if (tools is null) return false;
        // "*" means all MCP tools are allowed (canonical mcp.tool permission has no tool list)
        if (tools.Mcp.Contains("*", StringComparer.Ordinal)) return true;
        return tools.Mcp.Contains(toolId, StringComparer.Ordinal);
    }

    // ── Canonical permission conversion ───────────────────────────────────────

    private static AgentPermissions ParseCanonical(Dictionary<string, JsonElement>? perms)
    {
        if (perms is null) return new AgentPermissions();

        NetworkPermissions? network = null;
        if (perms.TryGetValue("network.outbound", out var netElem))
        {
            var hosts = GetStringArray(netElem, "hosts");
            // Convert bare hostnames to URL patterns so existing UrlPatternMatch works
            var patterns = hosts.Select(h => h.Contains("://") ? h : $"https://{h}/*").ToArray();
            network = new NetworkPermissions { Allow = patterns };
        }

        FilesystemPermissions? filesystem = null;
        var readPaths  = perms.TryGetValue("fs.read",  out var fsRead)  ? GetStringArray(fsRead,  "paths") : [];
        var writePaths = perms.TryGetValue("fs.write", out var fsWrite) ? GetStringArray(fsWrite, "paths") : [];
        if (readPaths.Length > 0 || writePaths.Length > 0)
        {
            filesystem = new FilesystemPermissions { Read = readPaths, Write = writePaths };
        }

        EnvironmentPermissions? environment = null;
        if (perms.TryGetValue("env.read", out var envElem))
        {
            var vars = GetStringArray(envElem, "vars");
            if (vars.Length > 0)
                environment = new EnvironmentPermissions { Allow = vars };
        }

        // Canonical "mcp.tool" permission grants access to all MCP tools
        ToolPermissions? tools = null;
        if (perms.ContainsKey("mcp.tool"))
            tools = new ToolPermissions { Mcp = ["*"] };

        return new AgentPermissions
        {
            Network     = network,
            Filesystem  = filesystem,
            Environment = environment,
            Tools       = tools,
        };
    }

    private static string[] GetStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return [];
        if (!element.TryGetProperty(propertyName, out var arr)) return [];
        if (arr.ValueKind != JsonValueKind.Array) return [];

        var result = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                result.Add(item.GetString()!);
        }
        return [.. result];
    }

    // ── Pattern matching ──────────────────────────────────────────────────────

    private static bool UrlPatternMatch(string pattern, string input)
    {
        if (!pattern.Contains('*'))
            return string.Equals(pattern, input, StringComparison.Ordinal);

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", ".*") + "$";

        return Regex.IsMatch(input, regexPattern);
    }

    private static bool GlobMatch(string pattern, string input)
    {
        pattern = pattern.Replace('\\', '/');
        input   = input.Replace('\\', '/');

        if (!pattern.Contains('*'))
            return string.Equals(pattern, input, StringComparison.OrdinalIgnoreCase);

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/]*") + "$";

        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}
