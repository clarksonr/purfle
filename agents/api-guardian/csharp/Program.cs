using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purfle.Agents.ApiGuardian;

/// <summary>
/// API Guardian agent — IPC entry point.
/// Reads commands from stdin, calls MCP tools via the AIVM,
/// and writes structured status reports to stdout.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] DefaultEndpoints =
    [
        "https://api.example.com/health",
        "https://api.example.com/v1/status"
    ];

    private const int DefaultLatencyThresholdMs = 2000;
    private const int CriticalMultiplier = 3;
    private const int RetryDelayMs = 5000;

    public static async Task Main(string[] args)
    {
        await RunAsync(Console.In, Console.Out);
    }

    public static async Task RunAsync(TextReader input, TextWriter output)
    {
        var report = new StatusReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            Endpoints = []
        };

        var endpoints = DefaultEndpoints;

        // Read IPC command from stdin if available
        var command = await ReadCommandAsync(input);
        if (command?.Endpoints is { Length: > 0 })
        {
            endpoints = command.Endpoints;
        }

        var latencyThreshold = command?.LatencyThresholdMs ?? DefaultLatencyThresholdMs;

        foreach (var endpoint in endpoints)
        {
            var result = await CheckEndpointAsync(endpoint, latencyThreshold);
            report.Endpoints.Add(result);
        }

        report.Summary = BuildSummary(report.Endpoints);

        // Write structured report to stdout for AIVM consumption
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await output.WriteLineAsync(json);
        await output.FlushAsync();
    }

    private static async Task<AgentCommand?> ReadCommandAsync(TextReader input)
    {
        try
        {
            var line = await input.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
                return null;

            return JsonSerializer.Deserialize<AgentCommand>(line, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<EndpointResult> CheckEndpointAsync(string endpoint, int latencyThresholdMs)
    {
        var result = new EndpointResult
        {
            Endpoint = endpoint,
            CheckedAt = DateTimeOffset.UtcNow
        };

        // Health check
        var (statusCode, latencyMs, error) = await MeasureEndpointAsync(endpoint);

        if (error is not null && statusCode == 0)
        {
            // Retry once after delay
            await Task.Delay(RetryDelayMs);
            (statusCode, latencyMs, error) = await MeasureEndpointAsync(endpoint);
        }

        result.StatusCode = statusCode;
        result.LatencyMs = latencyMs;
        result.Error = error;

        // Determine status
        if (statusCode == 0)
        {
            result.Status = "down";
            result.Alerts.Add("Endpoint unreachable after retry");
        }
        else if (statusCode >= 500)
        {
            result.Status = "down";
            result.Alerts.Add($"Server error: HTTP {statusCode}");
        }
        else if (statusCode >= 400)
        {
            result.Status = "degraded";
            result.Alerts.Add($"Client error: HTTP {statusCode}");
        }
        else if (latencyMs > latencyThresholdMs * CriticalMultiplier)
        {
            result.Status = "degraded";
            result.Alerts.Add($"Critical latency: {latencyMs}ms exceeds {CriticalMultiplier}x threshold ({latencyThresholdMs}ms)");
        }
        else if (latencyMs > latencyThresholdMs)
        {
            result.Status = "degraded";
            result.Alerts.Add($"High latency: {latencyMs}ms exceeds threshold ({latencyThresholdMs}ms)");
        }
        else
        {
            result.Status = "healthy";
        }

        // Schema diff placeholder — in production this calls the MCP schema-diff tool
        result.SchemaDiff = "no changes";

        return result;
    }

    private static async Task<(int StatusCode, long LatencyMs, string? Error)> MeasureEndpointAsync(string endpoint)
    {
        try
        {
            // In production, this sends an IPC request to the AIVM which calls the
            // health-check and latency-check MCP tools. For standalone execution,
            // we make a direct HTTP call.
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync(endpoint);
            sw.Stop();

            return ((int)response.StatusCode, sw.ElapsedMilliseconds, null);
        }
        catch (TaskCanceledException)
        {
            return (0, 0, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            return (0, 0, ex.Message);
        }
    }

    private static string BuildSummary(List<EndpointResult> endpoints)
    {
        var healthy = endpoints.Count(e => e.Status == "healthy");
        var degraded = endpoints.Count(e => e.Status == "degraded");
        var down = endpoints.Count(e => e.Status == "down");
        var total = endpoints.Count;

        if (down > 0)
            return $"{down}/{total} endpoint(s) DOWN. Immediate attention required.";
        if (degraded > 0)
            return $"{degraded}/{total} endpoint(s) degraded. Review alerts.";
        return $"All {total} endpoint(s) healthy.";
    }
}

// ── IPC Models ──────────────────────────────────────────────────────

public sealed class AgentCommand
{
    public string[]? Endpoints { get; set; }
    public int? LatencyThresholdMs { get; set; }
}

public sealed class StatusReport
{
    public DateTimeOffset Timestamp { get; set; }
    public string? Summary { get; set; }
    public List<EndpointResult> Endpoints { get; set; } = [];
}

public sealed class EndpointResult
{
    public string Endpoint { get; set; } = "";
    public string Status { get; set; } = "unknown";
    public int StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public string? Error { get; set; }
    public string? SchemaDiff { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public List<string> Alerts { get; set; } = [];
}
