using Purfle.IdentityHub.Core.Models;

namespace Purfle.IdentityHub.Core.Services;

/// <summary>
/// Agent registry — manages the catalog of published agent manifests.
/// </summary>
public interface IAgentRegistry
{
    Task<RegistryEntry?> GetByAgentIdAsync(string agentId, CancellationToken ct = default);
    Task<IReadOnlyList<RegistryEntry>> SearchAsync(string? term, int page = 0, int pageSize = 20, CancellationToken ct = default);
    Task<RegistryEntry> RegisterAsync(RegistryEntry entry, CancellationToken ct = default);
}
