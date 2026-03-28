using System.Text.Json;

namespace Purfle.Marketplace.Storage.Json.Infrastructure;

/// <summary>
/// Generic in-memory cached JSON file store with SemaphoreSlim for thread-safety.
/// Entire collection is memory-resident; written through on every mutation.
/// </summary>
internal sealed class JsonDocumentStore<T>
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<T> _items = [];
    private bool _loaded;

    public JsonDocumentStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return _items.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> FindAsync(Func<T, bool> predicate, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return _items.FirstOrDefault(predicate);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<T>> WhereAsync(Func<T, bool> predicate, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return _items.Where(predicate).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> AnyAsync(Func<T, bool> predicate, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return _items.Any(predicate);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> CountAsync(Func<T, bool>? predicate = null, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return predicate is null ? _items.Count : _items.Count(predicate);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddAsync(T item, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            _items.Add(item);
            await SaveAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(Func<T, bool> predicate, T updated, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            var index = _items.FindIndex(new Predicate<T>(predicate));
            if (index >= 0)
            {
                _items[index] = updated;
                await SaveAsync(ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(Func<T, bool> predicate, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            var index = _items.FindIndex(new Predicate<T>(predicate));
            if (index >= 0)
            {
                _items.RemoveAt(index);
                await SaveAsync(ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Provides LINQ query access to the in-memory collection under the lock.
    /// The query function runs synchronously against the cached list.
    /// </summary>
    public async Task<TResult> QueryAsync<TResult>(Func<IReadOnlyList<T>, TResult> query, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return query(_items);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_loaded) return;

            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath, ct);
                _items = JsonSerializer.Deserialize<List<T>>(json, JsonSerializerOptionsProvider.Default) ?? [];
            }

            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(_items, JsonSerializerOptionsProvider.Default);
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
