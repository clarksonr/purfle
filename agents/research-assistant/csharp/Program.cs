using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Agents.ResearchAssistant;

/// <summary>
/// IPC stdin/stdout agent for Purfle AIVM.
/// Reads a JSON command from stdin, executes a research workflow via MCP tool
/// calls over stdout/stdin, and returns a structured research report.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static async Task Main(string[] args)
    {
        var input = await Console.In.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(input))
        {
            SendError("No input received on stdin.");
            return;
        }

        IpcCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<IpcCommand>(input, JsonOptions);
        }
        catch (JsonException ex)
        {
            SendError($"Invalid JSON input: {ex.Message}");
            return;
        }

        if (command is null || string.IsNullOrWhiteSpace(command.Query))
        {
            SendError("Command must include a non-empty 'query' field.");
            return;
        }

        await ExecuteResearchWorkflow(command.Query, command.MaxSources ?? 5);
    }

    private static async Task ExecuteResearchWorkflow(string query, int maxSources)
    {
        // Step 1: Search the web
        var searchResults = await CallTool("research/web-search", new
        {
            query,
            max_results = maxSources * 2
        });

        if (searchResults is null || searchResults.Results.Count == 0)
        {
            SendResult(new ResearchReport
            {
                Topic = query,
                Summary = "No search results found for the given query.",
                Findings = [],
                OpenQuestions = ["The search returned no results. Try rephrasing the query."],
                Sources = []
            });
            return;
        }

        // Step 2: Fetch top pages
        var sources = new List<FetchedSource>();
        var fetchCount = Math.Min(maxSources, searchResults.Results.Count);

        for (var i = 0; i < fetchCount; i++)
        {
            var result = searchResults.Results[i];
            var pageContent = await CallFetchPage(result.Url);

            sources.Add(new FetchedSource
            {
                Title = result.Title,
                Url = result.Url,
                Snippet = result.Snippet,
                Content = pageContent
            });
        }

        // Step 3: Build the report and send it back for LLM synthesis
        var report = new ResearchReport
        {
            Topic = query,
            Summary = $"Research completed. Gathered {sources.Count} sources for: {query}",
            Findings = sources.Select(s => new Finding
            {
                Subtopic = s.Title,
                Content = TruncateContent(s.Content, 2000),
                CitationUrl = s.Url
            }).ToList(),
            OpenQuestions = ["LLM synthesis pending — the AIVM will produce the final narrative."],
            Sources = sources.Select(s => new SourceRef
            {
                Title = s.Title,
                Url = s.Url,
                AccessedAt = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }).ToList()
        };

        SendResult(report);
    }

    /// <summary>
    /// Sends an MCP tool call request over stdout and reads the response from stdin.
    /// </summary>
    private static async Task<SearchResponse?> CallTool(string toolName, object parameters)
    {
        var request = new ToolCallRequest
        {
            Tool = toolName,
            Parameters = parameters
        };

        Console.WriteLine(JsonSerializer.Serialize(request, JsonOptions));

        var response = await Console.In.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(response))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SearchResponse>(response, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Calls the research/fetch-page tool and returns the page text content.
    /// </summary>
    private static async Task<string> CallFetchPage(string url)
    {
        var request = new ToolCallRequest
        {
            Tool = "research/fetch-page",
            Parameters = new { url }
        };

        Console.WriteLine(JsonSerializer.Serialize(request, JsonOptions));

        var response = await Console.In.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        try
        {
            var page = JsonSerializer.Deserialize<FetchPageResponse>(response, JsonOptions);
            return page?.Content ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void SendResult(ResearchReport report)
    {
        var envelope = new IpcResponse
        {
            Status = "done",
            Report = report
        };
        Console.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static void SendError(string message)
    {
        var envelope = new IpcResponse
        {
            Status = "error",
            Error = message
        };
        Console.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content;
        return content[..maxLength] + "...";
    }
}

// ----- IPC message types -----

public sealed class IpcCommand
{
    public string? Query { get; set; }
    public int? MaxSources { get; set; }
}

public sealed class ToolCallRequest
{
    public string Tool { get; set; } = string.Empty;
    public object? Parameters { get; set; }
}

public sealed class SearchResponse
{
    public List<SearchResult> Results { get; set; } = [];
}

public sealed class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

public sealed class FetchPageResponse
{
    public string Content { get; set; } = string.Empty;
}

public sealed class FetchedSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class ResearchReport
{
    public string Topic { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<Finding> Findings { get; set; } = [];
    public List<string> OpenQuestions { get; set; } = [];
    public List<SourceRef> Sources { get; set; } = [];
}

public sealed class Finding
{
    public string Subtopic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string CitationUrl { get; set; } = string.Empty;
}

public sealed class SourceRef
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string AccessedAt { get; set; } = string.Empty;
}

public sealed class IpcResponse
{
    public string Status { get; set; } = string.Empty;
    public ResearchReport? Report { get; set; }
    public string? Error { get; set; }
}
