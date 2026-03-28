using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.OpenIddict;

public sealed class JsonAuthorizationStore : IOpenIddictAuthorizationStore<OpenIddictJsonAuthorization>
{
    private readonly JsonDocumentStore<OpenIddictJsonAuthorization> _store;

    public JsonAuthorizationStore(string dataDirectory)
    {
        _store = new JsonDocumentStore<OpenIddictJsonAuthorization>(
            Path.Combine(dataDirectory, "openiddict", "authorizations.json"));
    }

    public async ValueTask<long> CountAsync(CancellationToken ct)
        => await _store.CountAsync(ct: ct);

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictJsonAuthorization>, IQueryable<TResult>> query, CancellationToken ct)
        => throw new NotSupportedException();

    public async ValueTask CreateAsync(OpenIddictJsonAuthorization auth, CancellationToken ct)
        => await _store.AddAsync(auth, ct);

    public async ValueTask DeleteAsync(OpenIddictJsonAuthorization auth, CancellationToken ct)
        => await _store.RemoveAsync(a => a.Id == auth.Id, ct);

    public async ValueTask<OpenIddictJsonAuthorization?> FindByIdAsync(string id, CancellationToken ct)
        => await _store.FindAsync(a => a.Id == id, ct);

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindByApplicationIdAsync(
        string id, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.ApplicationId == id, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindBySubjectAsync(
        string subject, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.Subject == subject, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindAsync(
        string subject, string client, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.Subject == subject && a.ApplicationId == client, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindAsync(
        string subject, string client, string status, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a =>
            a.Subject == subject && a.ApplicationId == client && a.Status == status, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindAsync(
        string subject, string client, string status, string type, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a =>
            a.Subject == subject && a.ApplicationId == client && a.Status == status && a.Type == type, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindAsync(
        string subject, string client, string status, string type,
        ImmutableArray<string> scopes, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a =>
            a.Subject == subject && a.ApplicationId == client && a.Status == status && a.Type == type
            && !scopes.Except(a.Scopes ?? []).Any(), ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> FindAsync(
        string? subject, string? client, string? status, string? type,
        ImmutableArray<string>? scopes, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a =>
            (subject is null || a.Subject == subject) &&
            (client is null || a.ApplicationId == client) &&
            (status is null || a.Status == status) &&
            (type is null || a.Type == type) &&
            (scopes is null || !scopes.Value.Except(a.Scopes ?? []).Any()), ct);
        foreach (var item in items) yield return item;
    }

    public ValueTask<string?> GetApplicationIdAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.ApplicationId);
    public ValueTask<string?> GetConcurrencyTokenAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.ConcurrencyToken);
    public ValueTask<DateTimeOffset?> GetCreationDateAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.CreationDate);
    public ValueTask<string?> GetIdAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.Id);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictJsonAuthorization auth, CancellationToken ct)
        => new(auth.Properties?.ToImmutableDictionary() ?? ImmutableDictionary<string, JsonElement>.Empty);

    public ValueTask<ImmutableArray<string>> GetScopesAsync(OpenIddictJsonAuthorization auth, CancellationToken ct)
        => new(auth.Scopes?.ToImmutableArray() ?? ImmutableArray<string>.Empty);

    public ValueTask<string?> GetStatusAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.Status);
    public ValueTask<string?> GetSubjectAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.Subject);
    public ValueTask<string?> GetTypeAsync(OpenIddictJsonAuthorization auth, CancellationToken ct) => new(auth.Type);
    public ValueTask<OpenIddictJsonAuthorization> InstantiateAsync(CancellationToken ct) => new(new OpenIddictJsonAuthorization());

    public async IAsyncEnumerable<OpenIddictJsonAuthorization> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var query = all.AsEnumerable();
        if (offset.HasValue) query = query.Skip(offset.Value);
        if (count.HasValue) query = query.Take(count.Value);
        foreach (var item in query) yield return item;
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonAuthorization>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
        => throw new NotSupportedException();

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var toRemove = all.Where(a =>
            a.CreationDate < threshold &&
            a.Status != OpenIddictConstants.Statuses.Valid).ToList();

        foreach (var item in toRemove)
            await _store.RemoveAsync(a => a.Id == item.Id, ct);

        return toRemove.Count;
    }

    public ValueTask SetApplicationIdAsync(OpenIddictJsonAuthorization auth, string? id, CancellationToken ct) { auth.ApplicationId = id; return default; }
    public ValueTask SetConcurrencyTokenAsync(OpenIddictJsonAuthorization auth, string? token, CancellationToken ct) { auth.ConcurrencyToken = token; return default; }
    public ValueTask SetCreationDateAsync(OpenIddictJsonAuthorization auth, DateTimeOffset? date, CancellationToken ct) { auth.CreationDate = date; return default; }

    public ValueTask SetPropertiesAsync(OpenIddictJsonAuthorization auth, ImmutableDictionary<string, JsonElement> properties, CancellationToken ct)
    {
        auth.Properties = properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return default;
    }

    public ValueTask SetScopesAsync(OpenIddictJsonAuthorization auth, ImmutableArray<string> scopes, CancellationToken ct) { auth.Scopes = [.. scopes]; return default; }
    public ValueTask SetStatusAsync(OpenIddictJsonAuthorization auth, string? status, CancellationToken ct) { auth.Status = status; return default; }
    public ValueTask SetSubjectAsync(OpenIddictJsonAuthorization auth, string? subject, CancellationToken ct) { auth.Subject = subject; return default; }
    public ValueTask SetTypeAsync(OpenIddictJsonAuthorization auth, string? type, CancellationToken ct) { auth.Type = type; return default; }

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var toRevoke = all.Where(a =>
            (subject is null || a.Subject == subject) &&
            (client is null || a.ApplicationId == client) &&
            (status is null || a.Status == status) &&
            (type is null || a.Type == type)).ToList();

        foreach (var item in toRevoke)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(a => a.Id == item.Id, item, ct);
        }
        return toRevoke.Count;
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string id, CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.ApplicationId == id, ct);
        foreach (var item in items)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(a => a.Id == item.Id, item, ct);
        }
        return items.Count;
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.Subject == subject, ct);
        foreach (var item in items)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(a => a.Id == item.Id, item, ct);
        }
        return items.Count;
    }

    public async ValueTask UpdateAsync(OpenIddictJsonAuthorization auth, CancellationToken ct)
        => await _store.UpdateAsync(a => a.Id == auth.Id, auth, ct);

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonAuthorization>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        return query(all.AsQueryable(), state).FirstOrDefault();
    }
}
