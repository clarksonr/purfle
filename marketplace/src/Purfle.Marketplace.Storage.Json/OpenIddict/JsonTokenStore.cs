using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.OpenIddict;

public sealed class JsonTokenStore : IOpenIddictTokenStore<OpenIddictJsonToken>
{
    private readonly JsonDocumentStore<OpenIddictJsonToken> _store;

    public JsonTokenStore(string dataDirectory)
    {
        _store = new JsonDocumentStore<OpenIddictJsonToken>(
            Path.Combine(dataDirectory, "openiddict", "tokens.json"));
    }

    public async ValueTask<long> CountAsync(CancellationToken ct)
        => await _store.CountAsync(ct: ct);

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictJsonToken>, IQueryable<TResult>> query, CancellationToken ct)
        => throw new NotSupportedException();

    public async ValueTask CreateAsync(OpenIddictJsonToken token, CancellationToken ct)
        => await _store.AddAsync(token, ct);

    public async ValueTask DeleteAsync(OpenIddictJsonToken token, CancellationToken ct)
        => await _store.RemoveAsync(t => t.Id == token.Id, ct);

    public async ValueTask<OpenIddictJsonToken?> FindByIdAsync(string id, CancellationToken ct)
        => await _store.FindAsync(t => t.Id == id, ct);

    public async IAsyncEnumerable<OpenIddictJsonToken> FindByApplicationIdAsync(
        string id, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(t => t.ApplicationId == id, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonToken> FindByAuthorizationIdAsync(
        string id, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(t => t.AuthorizationId == id, ct);
        foreach (var item in items) yield return item;
    }

    public async ValueTask<OpenIddictJsonToken?> FindByReferenceIdAsync(string id, CancellationToken ct)
        => await _store.FindAsync(t => t.ReferenceId == id, ct);

    public async IAsyncEnumerable<OpenIddictJsonToken> FindBySubjectAsync(
        string subject, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(t => t.Subject == subject, ct);
        foreach (var item in items) yield return item;
    }

    public ValueTask<string?> GetApplicationIdAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.ApplicationId);
    public ValueTask<string?> GetAuthorizationIdAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.AuthorizationId);
    public ValueTask<string?> GetConcurrencyTokenAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.ConcurrencyToken);
    public ValueTask<DateTimeOffset?> GetCreationDateAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.CreationDate);
    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.ExpirationDate);
    public ValueTask<string?> GetIdAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.Id);
    public ValueTask<string?> GetPayloadAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.Payload);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictJsonToken token, CancellationToken ct)
        => new(token.Properties?.ToImmutableDictionary() ?? ImmutableDictionary<string, JsonElement>.Empty);

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.RedemptionDate);
    public ValueTask<string?> GetReferenceIdAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.ReferenceId);
    public ValueTask<string?> GetStatusAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.Status);
    public ValueTask<string?> GetSubjectAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.Subject);
    public ValueTask<string?> GetTypeAsync(OpenIddictJsonToken token, CancellationToken ct) => new(token.Type);
    public ValueTask<OpenIddictJsonToken> InstantiateAsync(CancellationToken ct) => new(new OpenIddictJsonToken());

    public async IAsyncEnumerable<OpenIddictJsonToken> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var query = all.AsEnumerable();
        if (offset.HasValue) query = query.Skip(offset.Value);
        if (count.HasValue) query = query.Take(count.Value);
        foreach (var item in query) yield return item;
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonToken>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
        => throw new NotSupportedException();

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var toRemove = all.Where(t =>
            t.CreationDate < threshold &&
            t.Status != OpenIddictConstants.Statuses.Valid).ToList();

        foreach (var item in toRemove)
            await _store.RemoveAsync(t => t.Id == item.Id, ct);

        return toRemove.Count;
    }

    public ValueTask SetApplicationIdAsync(OpenIddictJsonToken token, string? id, CancellationToken ct) { token.ApplicationId = id; return default; }
    public ValueTask SetAuthorizationIdAsync(OpenIddictJsonToken token, string? id, CancellationToken ct) { token.AuthorizationId = id; return default; }
    public ValueTask SetConcurrencyTokenAsync(OpenIddictJsonToken token, string? ct2, CancellationToken ct) { token.ConcurrencyToken = ct2; return default; }
    public ValueTask SetCreationDateAsync(OpenIddictJsonToken token, DateTimeOffset? date, CancellationToken ct) { token.CreationDate = date; return default; }
    public ValueTask SetExpirationDateAsync(OpenIddictJsonToken token, DateTimeOffset? date, CancellationToken ct) { token.ExpirationDate = date; return default; }
    public ValueTask SetPayloadAsync(OpenIddictJsonToken token, string? payload, CancellationToken ct) { token.Payload = payload; return default; }

    public ValueTask SetPropertiesAsync(OpenIddictJsonToken token, ImmutableDictionary<string, JsonElement> properties, CancellationToken ct)
    {
        token.Properties = properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return default;
    }

    public ValueTask SetRedemptionDateAsync(OpenIddictJsonToken token, DateTimeOffset? date, CancellationToken ct) { token.RedemptionDate = date; return default; }
    public ValueTask SetReferenceIdAsync(OpenIddictJsonToken token, string? id, CancellationToken ct) { token.ReferenceId = id; return default; }
    public ValueTask SetStatusAsync(OpenIddictJsonToken token, string? status, CancellationToken ct) { token.Status = status; return default; }
    public ValueTask SetSubjectAsync(OpenIddictJsonToken token, string? subject, CancellationToken ct) { token.Subject = subject; return default; }
    public ValueTask SetTypeAsync(OpenIddictJsonToken token, string? type, CancellationToken ct) { token.Type = type; return default; }

    public async IAsyncEnumerable<OpenIddictJsonToken> FindAsync(
        string? subject, string? client, string? status, string? type,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(t =>
            (subject is null || t.Subject == subject) &&
            (client is null || t.ApplicationId == client) &&
            (status is null || t.Status == status) &&
            (type is null || t.Type == type), ct);
        foreach (var item in items) yield return item;
    }

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var toRevoke = all.Where(t =>
            (subject is null || t.Subject == subject) &&
            (client is null || t.ApplicationId == client) &&
            (status is null || t.Status == status) &&
            (type is null || t.Type == type)).ToList();

        foreach (var item in toRevoke)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(t => t.Id == item.Id, item, ct);
        }
        return toRevoke.Count;
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string id, CancellationToken ct)
    {
        var items = await _store.WhereAsync(t => t.ApplicationId == id, ct);
        foreach (var item in items)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(t => t.Id == item.Id, item, ct);
        }
        return items.Count;
    }

    public async ValueTask<long> RevokeByAuthorizationIdAsync(string id, CancellationToken ct)
    {
        var items = await _store.WhereAsync(t => t.AuthorizationId == id, ct);
        foreach (var item in items)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(t => t.Id == item.Id, item, ct);
        }
        return items.Count;
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken ct)
    {
        var items = await _store.WhereAsync(t => t.Subject == subject, ct);
        foreach (var item in items)
        {
            item.Status = OpenIddictConstants.Statuses.Revoked;
            await _store.UpdateAsync(t => t.Id == item.Id, item, ct);
        }
        return items.Count;
    }

    public async ValueTask UpdateAsync(OpenIddictJsonToken token, CancellationToken ct)
        => await _store.UpdateAsync(t => t.Id == token.Id, token, ct);

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonToken>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        return query(all.AsQueryable(), state).FirstOrDefault();
    }
}
