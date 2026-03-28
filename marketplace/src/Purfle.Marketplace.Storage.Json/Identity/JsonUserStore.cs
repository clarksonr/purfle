using Microsoft.AspNetCore.Identity;
using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Identity;

public sealed class JsonUserStore :
    IUserStore<Publisher>,
    IUserPasswordStore<Publisher>,
    IUserEmailStore<Publisher>,
    IUserSecurityStampStore<Publisher>
{
    private readonly JsonDocumentStore<Publisher> _store;

    public JsonUserStore(string dataDirectory)
    {
        _store = new JsonDocumentStore<Publisher>(Path.Combine(dataDirectory, "publishers.json"));
    }

    public void Dispose() { }

    // IUserStore
    public Task<string> GetUserIdAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(Publisher user, string? userName, CancellationToken ct) { user.UserName = userName; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(Publisher user, string? normalizedName, CancellationToken ct) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }

    public async Task<IdentityResult> CreateAsync(Publisher user, CancellationToken ct)
    {
        await _store.AddAsync(user, ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(Publisher user, CancellationToken ct)
    {
        await _store.UpdateAsync(p => p.Id == user.Id, user, ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(Publisher user, CancellationToken ct)
    {
        await _store.RemoveAsync(p => p.Id == user.Id, ct);
        return IdentityResult.Success;
    }

    public async Task<Publisher?> FindByIdAsync(string userId, CancellationToken ct)
        => await _store.FindAsync(p => p.Id == userId, ct);

    public async Task<Publisher?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
        => await _store.FindAsync(p => p.NormalizedUserName == normalizedUserName, ct);

    // IUserPasswordStore
    public Task SetPasswordHashAsync(Publisher user, string? passwordHash, CancellationToken ct) { user.PasswordHash = passwordHash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.PasswordHash is not null);

    // IUserEmailStore
    public Task SetEmailAsync(Publisher user, string? email, CancellationToken ct) { user.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(Publisher user, CancellationToken ct) => Task.FromResult(true);
    public Task SetEmailConfirmedAsync(Publisher user, bool confirmed, CancellationToken ct) => Task.CompletedTask;
    public async Task<Publisher?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
        => await _store.FindAsync(p => p.NormalizedEmail == normalizedEmail, ct);
    public Task<string?> GetNormalizedEmailAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(Publisher user, string? normalizedEmail, CancellationToken ct) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }

    // IUserSecurityStampStore
    public Task SetSecurityStampAsync(Publisher user, string stamp, CancellationToken ct) { user.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(Publisher user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);
}
