using System.Text.Json;
using System.Text.RegularExpressions;
using Purfle.Runtime.Sandbox;

namespace Purfle.Runtime.Tools;

/// <summary>
/// Executes the built-in tool calls (find_files, search_files, read_file, write_file,
/// http_get) on behalf of an agent, enforcing sandbox permissions before every operation.
/// Both the Anthropic and Gemini adapters delegate here instead of duplicating this logic.
/// </summary>
public sealed class BuiltInToolExecutor
{
    private readonly AgentSandbox _sandbox;
    private readonly HttpClient   _http;

    public BuiltInToolExecutor(AgentSandbox sandbox, HttpClient? http = null)
    {
        _sandbox = sandbox;
        _http    = http ?? new HttpClient();
    }

    /// <summary>
    /// Dispatch a tool call by name. Returns an error string for unknown tools rather
    /// than throwing, so the LLM can receive the error as a tool result.
    /// </summary>
    public async Task<string> ExecuteAsync(
        string toolName, JsonElement? args, CancellationToken ct = default)
    {
        switch (toolName)
        {
            case "find_files":
                return ExecuteFindFiles(
                    args.HasValue && args.Value.TryGetProperty("name_pattern", out var np)
                        ? np.GetString() : null);

            case "search_files":
                return ExecuteSearchFiles(
                    args?.GetProperty("query").GetString() ?? "",
                    args.HasValue && args.Value.TryGetProperty("file_pattern", out var fp)
                        ? fp.GetString() : null);

            case "read_file":
                return ExecuteReadFile(args?.GetProperty("path").GetString() ?? "");

            case "write_file":
                return ExecuteWriteFile(
                    args?.GetProperty("path").GetString() ?? "",
                    args?.GetProperty("content").GetString() ?? "");

            case "http_get":
                return await ExecuteHttpGetAsync(
                    args?.GetProperty("url").GetString() ?? "", ct);

            default:
                return $"Error: unknown built-in tool '{toolName}'.";
        }
    }

    // ── find_files ────────────────────────────────────────────────────────────

    private string ExecuteFindFiles(string? namePattern)
    {
        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        if (!Directory.Exists(downloadsDir))
            return $"Error: Downloads directory not found at '{downloadsDir}'.";

        // Bare word with no wildcard or extension → prefix search
        var raw = string.IsNullOrWhiteSpace(namePattern) ? "*" : namePattern.Trim();
        if (!raw.Contains('*') && !raw.Contains('?') && !raw.Contains('.'))
            raw = raw + "*";

        // Enumerate all files and filter by name — avoids Windows quirks where
        // Directory.EnumerateFiles with patterns like "*foo*" returns nothing.
        var regex = "^" + Regex.Escape(raw)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        var matches       = new List<string>();
        int totalScanned  = 0;
        int sandboxBlocked = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(downloadsDir, "*", SearchOption.AllDirectories))
            {
                totalScanned++;
                if (!Regex.IsMatch(Path.GetFileName(file), regex, RegexOptions.IgnoreCase))
                    continue;

                if (_sandbox.CanReadPath(file))
                    matches.Add(file);
                else
                    sandboxBlocked++;
            }
        }
        catch (Exception ex)
        {
            return $"Error: find failed — {ex.Message}";
        }

        if (matches.Count == 0)
        {
            int rootCount = 0;
            try { rootCount = Directory.GetFiles(downloadsDir).Length; } catch { }
            return $"No files matching '{raw}' found in '{downloadsDir}'. " +
                   $"Scanned={totalScanned}, Blocked={sandboxBlocked}, RootFiles={rootCount}.";
        }

        return $"{matches.Count} file(s) matching '{raw}':\n" + string.Join("\n", matches);
    }

    // ── search_files ──────────────────────────────────────────────────────────

    private string ExecuteSearchFiles(string query, string? filePattern)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query must not be empty.";

        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        if (!Directory.Exists(downloadsDir))
            return $"Error: Downloads directory not found at '{downloadsDir}'.";

        var pattern    = string.IsNullOrWhiteSpace(filePattern) ? "*" : filePattern;
        var results    = new List<string>();
        const int MaxMatches = 50;
        bool truncated = false;

        try
        {
            foreach (var file in Directory.EnumerateFiles(downloadsDir, pattern, SearchOption.AllDirectories))
            {
                if (!_sandbox.CanReadPath(file)) continue;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }

                foreach (var (line, idx) in lines.Select((l, i) => (l, i)))
                {
                    if (!line.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                    results.Add($"{file}:{idx + 1}: {line.Trim()}");
                    if (results.Count >= MaxMatches) { truncated = true; break; }
                }

                if (truncated) break;
            }
        }
        catch (Exception ex)
        {
            return $"Error: search failed — {ex.Message}";
        }

        if (results.Count == 0)
            return $"No matches found for '{query}' in Downloads.";

        var header = truncated
            ? $"First {MaxMatches} matches for '{query}' (results truncated):\n"
            : $"{results.Count} match(es) for '{query}':\n";

        return header + string.Join("\n", results);
    }

    // ── read_file ─────────────────────────────────────────────────────────────

    private string ExecuteReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Error: path must not be empty.";
        if (!_sandbox.CanReadPath(path))     return $"Error: permission denied — '{path}' not in filesystem.read allowlist.";
        if (!File.Exists(path))              return $"Error: file not found — '{path}'.";
        return File.ReadAllText(path);
    }

    // ── write_file ────────────────────────────────────────────────────────────

    private string ExecuteWriteFile(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Error: path must not be empty.";

        // Resolve to absolute path to catch relative traversal (../../etc)
        var resolvedPath = Path.GetFullPath(path);

        if (!_sandbox.CanWritePath(resolvedPath))
        {
            Console.Error.WriteLine(
                $"[Sandbox] BLOCKED out-of-bounds write: requested='{path}', " +
                $"resolved='{resolvedPath}', allowed={_sandbox.GetWritePathsSummary()}");
            return $"Error: permission denied — write to '{resolvedPath}' blocked. " +
                   $"Path is outside the sandbox write allowlist ({_sandbox.GetWritePathsSummary()}).";
        }

        var dir = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(resolvedPath, content);
        return "OK";
    }

    // ── http_get ──────────────────────────────────────────────────────────────

    private async Task<string> ExecuteHttpGetAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return "Error: url must not be empty.";
        if (!_sandbox.CanAccessUrl(url))    return $"Error: permission denied — '{url}' not in network.allow list.";
        try   { return await _http.GetStringAsync(url, ct); }
        catch (Exception ex) { return $"Error: HTTP GET failed — {ex.Message}"; }
    }
}
