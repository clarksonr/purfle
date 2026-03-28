using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Repositories;

public sealed class JsonPublisherRepository : IPublisherRepository
{
    private readonly JsonDocumentStore<Publisher> _store;

    public JsonPublisherRepository(string dataDirectory)
    {
        _store = new JsonDocumentStore<Publisher>(Path.Combine(dataDirectory, "publishers.json"));
    }

    public async Task<Publisher?> FindByIdAsync(string id, CancellationToken ct)
        => await _store.FindAsync(p => p.Id == id, ct);

    public async Task<Publisher?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.ToUpperInvariant();
        return await _store.FindAsync(p => p.NormalizedEmail == normalized, ct);
    }

    public async Task<Publisher?> FindByNameAsync(string userName, CancellationToken ct)
    {
        var normalized = userName.ToUpperInvariant();
        return await _store.FindAsync(p => p.NormalizedUserName == normalized, ct);
    }

    public async Task CreateAsync(Publisher publisher, CancellationToken ct)
        => await _store.AddAsync(publisher, ct);

    public async Task UpdateAsync(Publisher publisher, CancellationToken ct)
        => await _store.UpdateAsync(p => p.Id == publisher.Id, publisher, ct);

    public async Task DeleteAsync(Publisher publisher, CancellationToken ct)
        => await _store.RemoveAsync(p => p.Id == publisher.Id, ct);
}
