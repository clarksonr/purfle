using System.Text.Json;
using Purfle.IdentityHub.Core.Models;
using Purfle.IdentityHub.Core.Services;

namespace Purfle.IdentityHub.Core.Implementations;

/// <summary>
/// JSON file-backed agent registry. Stores registry entries as JSON files
/// in a configurable directory.
/// </summary>
public sealed class JsonFileAgentRegistry : IAgentRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _storageDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileAgentRegistry(string storageDir)
    {
        _storageDir = storageDir;
        Directory.CreateDirectory(_storageDir);
    }

    public async Task<RegistryEntry?> GetByAgentIdAsync(string agentId, CancellationToken ct = default)
    {
        var entries = await LoadAllAsync(ct);
        return entries.FirstOrDefault(e => e.AgentId == agentId);
    }

    public async Task<IReadOnlyList<RegistryEntry>> SearchAsync(string? term, int page = 0, int pageSize = 20, CancellationToken ct = default)
    {
        var entries = await LoadAllAsync(ct);

        if (!string.IsNullOrWhiteSpace(term))
        {
            entries = entries.Where(e =>
                e.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.AgentId.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return entries
            .OrderByDescending(e => e.RegisteredAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<RegistryEntry> RegisterAsync(RegistryEntry entry, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            entry.RegisteredAt = DateTimeOffset.UtcNow;
            entry.UpdatedAt = DateTimeOffset.UtcNow;

            var filePath = Path.Combine(_storageDir, $"{entry.Id}.json");
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            return entry;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<RegistryEntry>> LoadAllAsync(CancellationToken ct)
    {
        var entries = new List<RegistryEntry>();
        if (!Directory.Exists(_storageDir)) return entries;

        foreach (var file in Directory.GetFiles(_storageDir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var entry = JsonSerializer.Deserialize<RegistryEntry>(json, JsonOptions);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }
}
