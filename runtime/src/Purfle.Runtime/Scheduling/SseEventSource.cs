namespace Purfle.Runtime.Scheduling;

/// <summary>
/// Production SSE-based <see cref="IEventSource"/> that connects to an MCP server's
/// Server-Sent Events stream and fires <see cref="OnEvent"/> when a matching topic
/// event arrives.
///
/// Reconnects automatically with exponential backoff (1s → 60s, with jitter)
/// if the connection drops. Respects cancellation for clean disconnect.
/// </summary>
public sealed class SseEventSource : IEventSource
{
    private readonly string _serverUrl;
    private readonly string _topic;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    /// <summary>Minimum backoff delay on reconnect.</summary>
    internal static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);

    /// <summary>Maximum backoff delay on reconnect.</summary>
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    public event Action? OnEvent;

    /// <param name="serverUrl">Base URL of the MCP server (e.g. <c>http://localhost:9999</c>).</param>
    /// <param name="topic">The event topic to subscribe to. Non-matching events are ignored.</param>
    /// <param name="httpClient">Optional HttpClient for testing. If null, a new one is created.</param>
    public SseEventSource(string serverUrl, string topic, HttpClient? httpClient = null)
    {
        _serverUrl  = serverUrl.TrimEnd('/');
        _topic      = topic;
        _ownsClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readLoop = ReadLoopAsync(_cts.Token);
        Console.Error.WriteLine(
            $"[SseEventSource] Connected to {_serverUrl} for topic '{_topic}'");
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        Console.Error.WriteLine($"[SseEventSource] Disconnecting from {_serverUrl}");
        _cts?.Cancel();
        if (_readLoop is not null)
        {
            try { await _readLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
        }
        _cts?.Dispose();
        _cts = null;
        if (_ownsClient)
            _httpClient.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var backoff = MinBackoff;
        var rng = new Random();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = $"{_serverUrl}/events?topic={Uri.EscapeDataString(_topic)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var response = await _httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var reader = new StreamReader(stream);

                // Reset backoff on successful connect
                backoff = MinBackoff;
                Console.Error.WriteLine($"[SseEventSource] Streaming from {_serverUrl}");

                string? eventType = null;
                string? data = null;

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line is null) break; // stream ended

                    if (line.Length == 0)
                    {
                        // Empty line = end of event
                        if (data is not null)
                        {
                            ProcessEvent(eventType, data);
                            eventType = null;
                            data = null;
                        }
                        continue;
                    }

                    if (line.StartsWith("event:", StringComparison.Ordinal))
                        eventType = line[6..].Trim();
                    else if (line.StartsWith("data:", StringComparison.Ordinal))
                        data = (data is null ? "" : data + "\n") + line[5..].Trim();
                    // Ignore comments (lines starting with ':') and other fields
                }

                // Stream ended cleanly — reconnect
                Console.Error.WriteLine(
                    $"[SseEventSource] Stream ended for {_serverUrl}, reconnecting...");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[SseEventSource] Connection to {_serverUrl} failed ({ex.Message}), retrying in {(int)backoff.TotalMilliseconds}ms");
            }

            if (ct.IsCancellationRequested) break;

            // Exponential backoff with jitter
            var jitter = TimeSpan.FromMilliseconds(rng.Next(0, (int)(backoff.TotalMilliseconds * 0.3)));
            await Task.Delay(backoff + jitter, ct).ConfigureAwait(false);
            backoff = TimeSpan.FromMilliseconds(
                Math.Min(backoff.TotalMilliseconds * 2, MaxBackoff.TotalMilliseconds));
        }
    }

    private void ProcessEvent(string? eventType, string data)
    {
        // If the event has a type, match it against the subscribed topic.
        // If no event type, treat it as a match (bare data-only event).
        if (eventType is not null &&
            !string.Equals(eventType, _topic, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Console.Error.WriteLine($"[SseEventSource] Event received for topic '{_topic}'");
        OnEvent?.Invoke();
    }
}

/// <summary>
/// Factory that creates <see cref="SseEventSource"/> instances. Register in DI
/// as <see cref="IEventSourceFactory"/>.
/// </summary>
public sealed class SseEventSourceFactory : IEventSourceFactory
{
    public IEventSource Create(string serverUrl, string topic)
        => new SseEventSource(serverUrl, topic);
}
