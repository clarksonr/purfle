using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Agents.NewsDigest;

/// <summary>
/// News Digest agent — IPC entry point.
/// Reads JSON commands from stdin, writes JSON responses to stdout.
/// On "execute": calls the news/headlines tool, categorizes results, formats a digest.
/// </summary>
public static class Program
{
    private static readonly string[] Categories =
        ["Tech", "Business", "Science", "Health", "Politics", "World", "Sports", "Entertainment"];

    public static async Task Main(string[] args)
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var message = JsonSerializer.Deserialize<IpcMessage>(line);
            if (message is null) continue;

            switch (message.Method)
            {
                case "execute":
                    await HandleExecute(writer);
                    break;

                case "ping":
                    await WriteResponse(writer, "pong", null);
                    break;

                default:
                    await WriteResponse(writer, "error", $"Unknown method: {message.Method}");
                    break;
            }
        }
    }

    private static async Task HandleExecute(StreamWriter writer)
    {
        // Step 1: Request headlines from the news MCP tool
        var toolCall = new IpcToolCall
        {
            Method = "tool_call",
            Tool = "mcp://localhost:8103/news/headlines",
            Arguments = new Dictionary<string, object>
            {
                ["category"] = "general",
                ["count"] = 20
            }
        };

        await writer.WriteLineAsync(JsonSerializer.Serialize(toolCall));

        // Step 2: Read tool result from stdin
        using var reader = new StreamReader(Console.OpenStandardInput());
        var resultLine = await reader.ReadLineAsync();

        List<NewsArticle> articles;
        if (resultLine is not null)
        {
            var toolResult = JsonSerializer.Deserialize<IpcToolResult>(resultLine);
            articles = toolResult?.Articles ?? [];
        }
        else
        {
            articles = [];
        }

        // Step 3: Categorize articles
        var categorized = new Dictionary<string, List<NewsArticle>>();
        foreach (var article in articles)
        {
            var category = ClassifyArticle(article);
            if (!categorized.ContainsKey(category))
                categorized[category] = [];
            categorized[category].Add(article);
        }

        // Step 4: Format the digest
        var digest = FormatDigest(categorized);

        // Step 5: Return done
        await WriteResponse(writer, "done", digest);
    }

    private static string ClassifyArticle(NewsArticle article)
    {
        var text = $"{article.Title} {article.Description}".ToLowerInvariant();

        if (text.Contains("tech") || text.Contains("software") || text.Contains("ai") ||
            text.Contains("cyber") || text.Contains("startup"))
            return "Tech";

        if (text.Contains("market") || text.Contains("stock") || text.Contains("economy") ||
            text.Contains("finance") || text.Contains("trade"))
            return "Business";

        if (text.Contains("science") || text.Contains("research") || text.Contains("study") ||
            text.Contains("space") || text.Contains("climate"))
            return "Science";

        if (text.Contains("health") || text.Contains("medical") || text.Contains("disease") ||
            text.Contains("vaccine") || text.Contains("hospital"))
            return "Health";

        if (text.Contains("politic") || text.Contains("election") || text.Contains("congress") ||
            text.Contains("senate") || text.Contains("president"))
            return "Politics";

        if (text.Contains("sport") || text.Contains("game") || text.Contains("team") ||
            text.Contains("league") || text.Contains("champion"))
            return "Sports";

        if (text.Contains("movie") || text.Contains("music") || text.Contains("celebrity") ||
            text.Contains("entertainment") || text.Contains("award"))
            return "Entertainment";

        return "World";
    }

    private static string FormatDigest(Dictionary<string, List<NewsArticle>> categorized)
    {
        var lines = new List<string>
        {
            $"# Daily News Digest",
            $"*Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*",
            ""
        };

        foreach (var category in Categories)
        {
            if (!categorized.TryGetValue(category, out var articles) || articles.Count == 0)
                continue;

            lines.Add($"## {category}");
            lines.Add("");

            foreach (var article in articles)
            {
                var source = string.IsNullOrEmpty(article.Source) ? "Unknown" : article.Source;
                var time = string.IsNullOrEmpty(article.PublishedAt) ? "" : $" ({article.PublishedAt})";
                var summary = string.IsNullOrEmpty(article.Description)
                    ? article.Title
                    : article.Description;

                lines.Add($"- **{article.Title}** -- {summary} *[{source}{time}]*");
            }

            lines.Add("");
        }

        return string.Join('\n', lines);
    }

    private static async Task WriteResponse(StreamWriter writer, string status, string? data)
    {
        var response = new IpcResponse { Status = status, Data = data };
        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
    }
}

public sealed class IpcMessage
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public sealed class IpcToolCall
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();
}

public sealed class IpcToolResult
{
    [JsonPropertyName("articles")]
    public List<NewsArticle> Articles { get; set; } = [];
}

public sealed class NewsArticle
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; } = "";
}

public sealed class IpcResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}
