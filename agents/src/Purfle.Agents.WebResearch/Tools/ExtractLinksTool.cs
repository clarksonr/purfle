using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Purfle.Sdk;

namespace Purfle.Agents.WebResearch.Tools;

/// <summary>
/// Extracts all unique hyperlinks from an HTML page and returns them as a
/// formatted list. Designed to be used after <c>http_get</c> fetches a page.
///
/// <para>
/// This tool performs in-memory HTML parsing only — no network calls, no
/// filesystem access. No sandbox checks are needed.
/// </para>
/// </summary>
public sealed class ExtractLinksTool : IAgentTool
{
    // Matches href="..." or href='...' attributes, capturing the URL value.
    private static readonly Regex s_hrefPattern =
        new(@"href\s*=\s*[""']([^""'#][^""']*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Name => "extract_links";

    public string Description =>
        "Extract all unique hyperlinks (href values) from an HTML string. " +
        "Use this after http_get to pull out links from a fetched page for further research. " +
        "Returns a numbered list of unique URLs found in the page.";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "html": {
              "type": "string",
              "description": "The HTML content to extract links from (typically the output of http_get)."
            },
            "base_url": {
              "type": "string",
              "description": "Optional base URL to resolve relative links against."
            }
          },
          "required": ["html"]
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

        var html    = root.TryGetProperty("html",     out var h) ? h.GetString() : null;
        var baseUrl = root.TryGetProperty("base_url", out var b) ? b.GetString() : null;

        if (string.IsNullOrWhiteSpace(html))
            return Task.FromResult("Error: 'html' is required.");

        var links = ExtractLinks(html, baseUrl);

        if (links.Count == 0)
            return Task.FromResult("No hyperlinks found in the provided HTML.");

        var sb = new StringBuilder();
        sb.AppendLine($"Found {links.Count} unique link(s):");
        for (int i = 0; i < links.Count; i++)
            sb.AppendLine($"  {i + 1,3}. {links[i]}");

        return Task.FromResult(sb.ToString());
    }

    private static List<string> ExtractLinks(string html, string? baseUrl)
    {
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<string>();

        foreach (Match match in s_hrefPattern.Matches(html))
        {
            var href = match.Groups[1].Value.Trim();

            // Skip mailto:, javascript:, data: URIs.
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Resolve relative URLs when a base is provided.
            if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(baseUrl) &&
                Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, href, out var resolved))
            {
                href = resolved.ToString();
            }

            if (seen.Add(href))
                links.Add(href);
        }

        return links;
    }
}
