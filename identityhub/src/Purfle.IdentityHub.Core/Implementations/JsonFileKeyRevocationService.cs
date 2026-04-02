using System.Text.Json;
using Purfle.IdentityHub.Core.Models;
using Purfle.IdentityHub.Core.Services;

namespace Purfle.IdentityHub.Core.Implementations;

/// <summary>
/// JSON file-backed key revocation service. Stores revocation records and
/// delegates key lookup to the marketplace SigningKey repository.
/// </summary>
public sealed class JsonFileKeyRevocationService : IKeyRevocationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _storageDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileKeyRevocationService(string storageDir)
    {
        _storageDir = storageDir;
        Directory.CreateDirectory(_storageDir);
    }

    public async Task<bool> IsRevokedAsync(string keyId, CancellationToken ct = default)
    {
        var revocations = await LoadAllAsync(ct);
        return revocations.Any(r => r.KeyId == keyId);
    }

    public async Task<RevocationRecord> RevokeAsync(string keyId, string reason, string? revokedBy = null, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var record = new RevocationRecord
            {
                KeyId = keyId,
                Reason = reason,
                RevokedBy = revokedBy,
                RevokedAt = DateTimeOffset.UtcNow,
            };

            var filePath = Path.Combine(_storageDir, $"{record.Id}.json");
            var json = JsonSerializer.Serialize(record, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            return record;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<RevocationRecord>> GetRevocationsAsync(CancellationToken ct = default)
    {
        return await LoadAllAsync(ct);
    }

    private async Task<List<RevocationRecord>> LoadAllAsync(CancellationToken ct)
    {
        var records = new List<RevocationRecord>();
        if (!Directory.Exists(_storageDir)) return records;

        foreach (var file in Directory.GetFiles(_storageDir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var record = JsonSerializer.Deserialize<RevocationRecord>(json, JsonOptions);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }
}
