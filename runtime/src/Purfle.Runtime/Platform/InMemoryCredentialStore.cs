using System.Collections.Concurrent;

namespace Purfle.Runtime.Platform;

/// <summary>
/// In-memory credential store for testing and unsupported platforms.
/// Credentials do not persist across process restarts.
/// </summary>
public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
