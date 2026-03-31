using System.Text;
using System.Text.Json;
using Purfle.Sdk;

namespace Purfle.Agents.FileSearch.Tools;

/// <summary>
/// Searches file contents within a directory tree for a query string and returns
/// matching lines with surrounding context — similar to grep.
///
/// <para>
/// The tool validates that <paramref name="directory"/> is within the user's
/// home directory as a defense-in-depth measure. The AIVM sandbox enforces
/// filesystem permissions at the adapter layer.
/// </para>
/// </summary>
public sealed class FileSearchTool : IAgentTool
{
    private const int MaxResults   = 50;
    private const int ContextLines = 2;

    public string Name => "search_files";

    public string Description =>
        "Search the contents of files in a directory for a query string. " +
        "Returns matching lines with file path, line number, and surrounding context. " +
        "Searches recursively through subdirectories. " +
        "Supported file types: .txt, .md, .cs, .json, .xml, .yaml, .yml, .log, .csv";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": {
              "type": "string",
              "description": "Absolute path to the directory to search."
            },
            "query": {
              "type": "string",
              "description": "Text to search for in file contents. Case-insensitive."
            },
            "file_pattern": {
              "type": "string",
              "description": "Optional glob pattern to filter files, e.g. '*.cs' or '*.md'. Defaults to all supported types."
            },
            "max_results": {
              "type": "integer",
              "description": "Maximum number of matching lines to return. Defaults to 20, maximum 50."
            }
          },
          "required": ["directory", "query"]
        }
        """;

    public Task<string> ExecuteAsync(string inputJson, CancellationToken ct = default)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(inputJson).RootElement;
        }
        catch
        {
            return Task.FromResult("Error: invalid JSON input.");
        }

        var directory  = root.TryGetProperty("directory",   out var d) ? d.GetString() : null;
        var query      = root.TryGetProperty("query",       out var q) ? q.GetString() : null;
        var pattern    = root.TryGetProperty("file_pattern",out var p) ? p.GetString() : null;
        var maxResults = root.TryGetProperty("max_results", out var m) && m.TryGetInt32(out var n)
                         ? Math.Clamp(n, 1, MaxResults) : 20;

        if (string.IsNullOrWhiteSpace(directory))
            return Task.FromResult("Error: 'directory' is required.");

        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult("Error: 'query' is required.");

        // Validate directory is within user home as a defense-in-depth measure.
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fullDir  = Path.GetFullPath(directory);
        if (!fullDir.StartsWith(userHome, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult($"Error: directory '{directory}' is outside the user home directory. " +
                                   "Only paths within the user home directory are permitted.");

        if (!Directory.Exists(fullDir))
            return Task.FromResult($"Error: directory not found — '{fullDir}'.");

        return Task.FromResult(Search(fullDir, query, pattern, maxResults, ct));
    }

    private static string Search(
        string directory, string query, string? pattern, int maxResults, CancellationToken ct)
    {
        var extensions = new HashSet<string>(
            [".txt", ".md", ".cs", ".json", ".xml", ".yaml", ".yml", ".log", ".csv"],
            StringComparer.OrdinalIgnoreCase);

        var searchPattern = pattern ?? "*";
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            return $"Error: could not enumerate files — {ex.Message}";
        }

        var results    = new StringBuilder();
        var matchCount = 0;
        var fileCount  = 0;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;
            if (matchCount >= maxResults) break;

            // Skip unsupported extensions when no explicit pattern is given.
            if (pattern is null && !extensions.Contains(Path.GetExtension(file)))
                continue;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch
            {
                continue; // Skip unreadable files silently.
            }

            fileCount++;
            var fileMatched = false;

            for (int i = 0; i < lines.Length && matchCount < maxResults; i++)
            {
                if (!lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!fileMatched)
                {
                    results.AppendLine();
                    results.AppendLine($"📄 {file}");
                    fileMatched = true;
                }

                // Print context lines before match.
                var start = Math.Max(0, i - ContextLines);
                var end   = Math.Min(lines.Length - 1, i + ContextLines);

                for (int j = start; j <= end; j++)
                {
                    var marker = j == i ? ">>>" : "   ";
                    results.AppendLine($"  {marker} {j + 1,5}: {lines[j]}");
                }

                results.AppendLine();
                matchCount++;
            }
        }

        if (matchCount == 0)
            return $"No matches found for '{query}' in '{directory}'.";

        var header = $"Found {matchCount} match(es) across {fileCount} file(s) " +
                     $"(searched '{directory}', query: '{query}'):";
        return header + Environment.NewLine + results;
    }
}
