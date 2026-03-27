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

    public AgentSandbox(AgentPermissions permissions)
    {
        _permissions = permissions;
    }

    /// <summary>
    /// Returns true if the agent may make an outbound request to <paramref name="url"/>.
    /// Deny entries take precedence over allow entries.
    /// </summary>
    public bool CanAccessUrl(string url)
    {
        var net = _permissions.Network;
        if (net is null) return false;

        // Deny takes precedence. URL patterns: * and ** both match any sequence
        // including slashes, because URL components have no meaningful glob boundary.
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

    /// <summary>Returns true if the agent may invoke MCP tool <paramref name="toolId"/>.</summary>
    public bool CanUseMcpTool(string toolId)
    {
        var tools = _permissions.Tools;
        if (tools is null) return false;
        return tools.Mcp.Contains(toolId, StringComparer.Ordinal);
    }

    /// <summary>
    /// URL pattern matching. Both <c>*</c> and <c>**</c> match any sequence of characters
    /// including slashes, since URL components have no meaningful path-segment boundary.
    /// Case-sensitive.
    /// </summary>
    private static bool UrlPatternMatch(string pattern, string input)
    {
        if (!pattern.Contains('*'))
            return string.Equals(pattern, input, StringComparison.Ordinal);

        // Both * and ** → .* for URL patterns
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", ".*") + "$";

        return Regex.IsMatch(input, regexPattern);
    }

    /// <summary>
    /// Filesystem glob matching. <c>*</c> matches within a path segment (no slashes);
    /// <c>**</c> matches across segments. Case-sensitive.
    /// </summary>
    private static bool GlobMatch(string pattern, string input)
    {
        if (!pattern.Contains('*'))
            return string.Equals(pattern, input, StringComparison.Ordinal);

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/\\]*") + "$";

        return Regex.IsMatch(input, regexPattern);
    }
}
