using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.OpenIddict;

public sealed class JsonApplicationStore : IOpenIddictApplicationStore<OpenIddictJsonApplication>
{
    private readonly JsonDocumentStore<OpenIddictJsonApplication> _store;

    public JsonApplicationStore(string dataDirectory)
    {
        _store = new JsonDocumentStore<OpenIddictJsonApplication>(
            Path.Combine(dataDirectory, "openiddict", "applications.json"));
    }

    public async ValueTask<long> CountAsync(CancellationToken ct)
        => await _store.CountAsync(ct: ct);

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictJsonApplication>, IQueryable<TResult>> query, CancellationToken ct)
        => throw new NotSupportedException();

    public async ValueTask CreateAsync(OpenIddictJsonApplication application, CancellationToken ct)
        => await _store.AddAsync(application, ct);

    public async ValueTask DeleteAsync(OpenIddictJsonApplication application, CancellationToken ct)
        => await _store.RemoveAsync(a => a.Id == application.Id, ct);

    public async ValueTask<OpenIddictJsonApplication?> FindByIdAsync(string id, CancellationToken ct)
        => await _store.FindAsync(a => a.Id == id, ct);

    public async ValueTask<OpenIddictJsonApplication?> FindByClientIdAsync(string clientId, CancellationToken ct)
        => await _store.FindAsync(a => a.ClientId == clientId, ct);

    public async IAsyncEnumerable<OpenIddictJsonApplication> FindByRedirectUriAsync(
        string uri, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.RedirectUris?.Contains(uri) == true, ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonApplication> FindByPostLogoutRedirectUriAsync(
        string uri, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(a => a.PostLogoutRedirectUris?.Contains(uri) == true, ct);
        foreach (var item in items) yield return item;
    }

    public ValueTask<string?> GetApplicationTypeAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.ApplicationType);
    public ValueTask<string?> GetClientIdAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.ClientId);
    public ValueTask<string?> GetClientSecretAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.ClientSecret);
    public ValueTask<string?> GetClientTypeAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.ClientType);
    public ValueTask<string?> GetConcurrencyTokenAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.ConcurrencyToken);
    public ValueTask<string?> GetConsentTypeAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.ConsentType);
    public ValueTask<string?> GetDisplayNameAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(OpenIddictJsonApplication app, CancellationToken ct)
    {
        if (app.DisplayNames is null)
            return new(ImmutableDictionary<CultureInfo, string>.Empty);
        var dict = app.DisplayNames.ToImmutableDictionary(
            kvp => new CultureInfo(kvp.Key),
            kvp => kvp.Value.GetString() ?? string.Empty);
        return new(dict);
    }

    public ValueTask<string?> GetIdAsync(OpenIddictJsonApplication app, CancellationToken ct) => new(app.Id);

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(OpenIddictJsonApplication app, CancellationToken ct)
    {
        if (app.JsonWebKeySet is null) return new((JsonWebKeySet?)null);
        return new(JsonWebKeySet.Create(app.JsonWebKeySet));
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(OpenIddictJsonApplication app, CancellationToken ct)
        => new(app.Permissions?.ToImmutableArray() ?? ImmutableArray<string>.Empty);

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(OpenIddictJsonApplication app, CancellationToken ct)
        => new(app.PostLogoutRedirectUris?.ToImmutableArray() ?? ImmutableArray<string>.Empty);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictJsonApplication app, CancellationToken ct)
        => new(app.Properties?.ToImmutableDictionary() ?? ImmutableDictionary<string, JsonElement>.Empty);

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(OpenIddictJsonApplication app, CancellationToken ct)
        => new(app.RedirectUris?.ToImmutableArray() ?? ImmutableArray<string>.Empty);

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(OpenIddictJsonApplication app, CancellationToken ct)
        => new(app.Requirements?.ToImmutableArray() ?? ImmutableArray<string>.Empty);

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(OpenIddictJsonApplication app, CancellationToken ct)
    {
        if (app.Settings is null)
            return new(ImmutableDictionary<string, string>.Empty);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(app.Settings);
        return new(dict?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty);
    }

    public ValueTask<OpenIddictJsonApplication> InstantiateAsync(CancellationToken ct) => new(new OpenIddictJsonApplication());

    public async IAsyncEnumerable<OpenIddictJsonApplication> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var query = all.AsEnumerable();
        if (offset.HasValue) query = query.Skip(offset.Value);
        if (count.HasValue) query = query.Take(count.Value);
        foreach (var item in query) yield return item;
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonApplication>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
        => throw new NotSupportedException();

    public ValueTask SetApplicationTypeAsync(OpenIddictJsonApplication app, string? type, CancellationToken ct) { app.ApplicationType = type; return default; }
    public ValueTask SetClientIdAsync(OpenIddictJsonApplication app, string? id, CancellationToken ct) { app.ClientId = id; return default; }
    public ValueTask SetClientSecretAsync(OpenIddictJsonApplication app, string? secret, CancellationToken ct) { app.ClientSecret = secret; return default; }
    public ValueTask SetClientTypeAsync(OpenIddictJsonApplication app, string? type, CancellationToken ct) { app.ClientType = type; return default; }
    public ValueTask SetConcurrencyTokenAsync(OpenIddictJsonApplication app, string? token, CancellationToken ct) { app.ConcurrencyToken = token; return default; }
    public ValueTask SetConsentTypeAsync(OpenIddictJsonApplication app, string? type, CancellationToken ct) { app.ConsentType = type; return default; }
    public ValueTask SetDisplayNameAsync(OpenIddictJsonApplication app, string? name, CancellationToken ct) { app.DisplayName = name; return default; }

    public ValueTask SetDisplayNamesAsync(OpenIddictJsonApplication app, ImmutableDictionary<CultureInfo, string> names, CancellationToken ct)
    {
        app.DisplayNames = names.ToDictionary(
            kvp => kvp.Key.Name,
            kvp => JsonSerializer.SerializeToElement(kvp.Value));
        return default;
    }

    public ValueTask SetJsonWebKeySetAsync(OpenIddictJsonApplication app, JsonWebKeySet? set, CancellationToken ct)
    {
        app.JsonWebKeySet = set?.ToString();
        return default;
    }

    public ValueTask SetPermissionsAsync(OpenIddictJsonApplication app, ImmutableArray<string> permissions, CancellationToken ct) { app.Permissions = [.. permissions]; return default; }
    public ValueTask SetPostLogoutRedirectUrisAsync(OpenIddictJsonApplication app, ImmutableArray<string> uris, CancellationToken ct) { app.PostLogoutRedirectUris = [.. uris]; return default; }

    public ValueTask SetPropertiesAsync(OpenIddictJsonApplication app, ImmutableDictionary<string, JsonElement> properties, CancellationToken ct)
    {
        app.Properties = properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return default;
    }

    public ValueTask SetRedirectUrisAsync(OpenIddictJsonApplication app, ImmutableArray<string> uris, CancellationToken ct) { app.RedirectUris = [.. uris]; return default; }
    public ValueTask SetRequirementsAsync(OpenIddictJsonApplication app, ImmutableArray<string> requirements, CancellationToken ct) { app.Requirements = [.. requirements]; return default; }

    public ValueTask SetSettingsAsync(OpenIddictJsonApplication app, ImmutableDictionary<string, string> settings, CancellationToken ct)
    {
        app.Settings = JsonSerializer.Serialize(settings);
        return default;
    }

    public async ValueTask UpdateAsync(OpenIddictJsonApplication app, CancellationToken ct)
        => await _store.UpdateAsync(a => a.Id == app.Id, app, ct);

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonApplication>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        return query(all.AsQueryable(), state).FirstOrDefault();
    }
}
