using System.Text.Json;
using Purfle.IdentityHub.Core.Models;
using Purfle.IdentityHub.Core.Services;

namespace Purfle.IdentityHub.Core.Implementations;

/// <summary>
/// JSON file-backed trust attestation service.
/// </summary>
public sealed class JsonFileTrustService : ITrustService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _storageDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileTrustService(string storageDir)
    {
        _storageDir = storageDir;
        Directory.CreateDirectory(_storageDir);
    }

    public async Task<IReadOnlyList<TrustAttestation>> GetAttestationsAsync(string agentId, CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return all.Where(a => a.AgentId == agentId).OrderByDescending(a => a.IssuedAt).ToList();
    }

    public async Task<TrustAttestation> IssueAsync(TrustAttestation attestation, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            attestation.IssuedAt = DateTimeOffset.UtcNow;

            var filePath = Path.Combine(_storageDir, $"{attestation.Id}.json");
            var json = JsonSerializer.Serialize(attestation, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            return attestation;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<TrustAttestation>> LoadAllAsync(CancellationToken ct)
    {
        var attestations = new List<TrustAttestation>();
        if (!Directory.Exists(_storageDir)) return attestations;

        foreach (var file in Directory.GetFiles(_storageDir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var attestation = JsonSerializer.Deserialize<TrustAttestation>(json, JsonOptions);
            if (attestation is not null)
                attestations.Add(attestation);
        }

        return attestations;
    }
}
