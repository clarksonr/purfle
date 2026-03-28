using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.OpenIddict;

public sealed class JsonScopeStore : IOpenIddictScopeStore<OpenIddictJsonScope>
{
    private readonly JsonDocumentStore<OpenIddictJsonScope> _store;

    public JsonScopeStore(string dataDirectory)
    {
        _store = new JsonDocumentStore<OpenIddictJsonScope>(
            Path.Combine(dataDirectory, "openiddict", "scopes.json"));
    }

    public async ValueTask<long> CountAsync(CancellationToken ct)
        => await _store.CountAsync(ct: ct);

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictJsonScope>, IQueryable<TResult>> query, CancellationToken ct)
        => throw new NotSupportedException();

    public async ValueTask CreateAsync(OpenIddictJsonScope scope, CancellationToken ct)
        => await _store.AddAsync(scope, ct);

    public async ValueTask DeleteAsync(OpenIddictJsonScope scope, CancellationToken ct)
        => await _store.RemoveAsync(s => s.Id == scope.Id, ct);

    public async ValueTask<OpenIddictJsonScope?> FindByIdAsync(string id, CancellationToken ct)
        => await _store.FindAsync(s => s.Id == id, ct);

    public async ValueTask<OpenIddictJsonScope?> FindByNameAsync(string name, CancellationToken ct)
        => await _store.FindAsync(s => s.Name == name, ct);

    public async IAsyncEnumerable<OpenIddictJsonScope> FindByNamesAsync(
        ImmutableArray<string> names, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(s => s.Name is not null && names.Contains(s.Name), ct);
        foreach (var item in items) yield return item;
    }

    public async IAsyncEnumerable<OpenIddictJsonScope> FindByResourceAsync(
        string resource, [EnumeratorCancellation] CancellationToken ct)
    {
        var items = await _store.WhereAsync(s => s.Resources?.Contains(resource) == true, ct);
        foreach (var item in items) yield return item;
    }

    public ValueTask<string?> GetConcurrencyTokenAsync(OpenIddictJsonScope scope, CancellationToken ct) => new(scope.ConcurrencyToken);
    public ValueTask<string?> GetDescriptionAsync(OpenIddictJsonScope scope, CancellationToken ct) => new(scope.Description);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(OpenIddictJsonScope scope, CancellationToken ct)
    {
        if (scope.Descriptions is null)
            return new(ImmutableDictionary<CultureInfo, string>.Empty);
        var dict = scope.Descriptions.ToImmutableDictionary(
            kvp => new CultureInfo(kvp.Key),
            kvp => kvp.Value.GetString() ?? string.Empty);
        return new(dict);
    }

    public ValueTask<string?> GetDisplayNameAsync(OpenIddictJsonScope scope, CancellationToken ct) => new(scope.DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(OpenIddictJsonScope scope, CancellationToken ct)
    {
        if (scope.DisplayNames is null)
            return new(ImmutableDictionary<CultureInfo, string>.Empty);
        var dict = scope.DisplayNames.ToImmutableDictionary(
            kvp => new CultureInfo(kvp.Key),
            kvp => kvp.Value.GetString() ?? string.Empty);
        return new(dict);
    }

    public ValueTask<string?> GetIdAsync(OpenIddictJsonScope scope, CancellationToken ct) => new(scope.Id);
    public ValueTask<string?> GetNameAsync(OpenIddictJsonScope scope, CancellationToken ct) => new(scope.Name);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictJsonScope scope, CancellationToken ct)
        => new(scope.Properties?.ToImmutableDictionary() ?? ImmutableDictionary<string, JsonElement>.Empty);

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(OpenIddictJsonScope scope, CancellationToken ct)
        => new(scope.Resources?.ToImmutableArray() ?? ImmutableArray<string>.Empty);

    public ValueTask<OpenIddictJsonScope> InstantiateAsync(CancellationToken ct) => new(new OpenIddictJsonScope());

    public async IAsyncEnumerable<OpenIddictJsonScope> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        var query = all.AsEnumerable();
        if (offset.HasValue) query = query.Skip(offset.Value);
        if (count.HasValue) query = query.Take(count.Value);
        foreach (var item in query) yield return item;
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonScope>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
        => throw new NotSupportedException();

    public ValueTask SetConcurrencyTokenAsync(OpenIddictJsonScope scope, string? token, CancellationToken ct) { scope.ConcurrencyToken = token; return default; }
    public ValueTask SetDescriptionAsync(OpenIddictJsonScope scope, string? desc, CancellationToken ct) { scope.Description = desc; return default; }

    public ValueTask SetDescriptionsAsync(OpenIddictJsonScope scope, ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken ct)
    {
        scope.Descriptions = descriptions.ToDictionary(
            kvp => kvp.Key.Name,
            kvp => JsonSerializer.SerializeToElement(kvp.Value));
        return default;
    }

    public ValueTask SetDisplayNameAsync(OpenIddictJsonScope scope, string? name, CancellationToken ct) { scope.DisplayName = name; return default; }

    public ValueTask SetDisplayNamesAsync(OpenIddictJsonScope scope, ImmutableDictionary<CultureInfo, string> names, CancellationToken ct)
    {
        scope.DisplayNames = names.ToDictionary(
            kvp => kvp.Key.Name,
            kvp => JsonSerializer.SerializeToElement(kvp.Value));
        return default;
    }

    public ValueTask SetNameAsync(OpenIddictJsonScope scope, string? name, CancellationToken ct) { scope.Name = name; return default; }

    public ValueTask SetPropertiesAsync(OpenIddictJsonScope scope, ImmutableDictionary<string, JsonElement> properties, CancellationToken ct)
    {
        scope.Properties = properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return default;
    }

    public ValueTask SetResourcesAsync(OpenIddictJsonScope scope, ImmutableArray<string> resources, CancellationToken ct) { scope.Resources = [.. resources]; return default; }

    public async ValueTask UpdateAsync(OpenIddictJsonScope scope, CancellationToken ct)
        => await _store.UpdateAsync(s => s.Id == scope.Id, scope, ct);

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictJsonScope>, TState, IQueryable<TResult>> query,
        TState state, CancellationToken ct)
    {
        var all = await _store.GetAllAsync(ct);
        return query(all.AsQueryable(), state).FirstOrDefault();
    }
}
