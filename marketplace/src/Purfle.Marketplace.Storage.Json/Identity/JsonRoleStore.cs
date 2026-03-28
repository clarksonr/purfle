using Microsoft.AspNetCore.Identity;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Identity;

/// <summary>
/// Minimal role store. Identity requires it even though roles aren't used yet.
/// </summary>
public sealed class JsonRoleStore : IRoleStore<IdentityRole>
{
    private readonly JsonDocumentStore<IdentityRole> _store;

    public JsonRoleStore(string dataDirectory)
    {
        _store = new JsonDocumentStore<IdentityRole>(Path.Combine(dataDirectory, "roles.json"));
    }

    public void Dispose() { }

    public async Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken ct)
    {
        await _store.AddAsync(role, ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken ct)
    {
        await _store.UpdateAsync(r => r.Id == role.Id, role, ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken ct)
    {
        await _store.RemoveAsync(r => r.Id == role.Id, ct);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken ct) => Task.FromResult(role.Id);
    public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken ct) => Task.FromResult(role.Name);
    public Task SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken ct) { role.Name = roleName; return Task.CompletedTask; }
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken ct) => Task.FromResult(role.NormalizedName);
    public Task SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken ct) { role.NormalizedName = normalizedName; return Task.CompletedTask; }

    public async Task<IdentityRole?> FindByIdAsync(string roleId, CancellationToken ct)
        => await _store.FindAsync(r => r.Id == roleId, ct);

    public async Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken ct)
        => await _store.FindAsync(r => r.NormalizedName == normalizedRoleName, ct);
}
