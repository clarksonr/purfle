using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Infrastructure;

namespace Purfle.Marketplace.Storage.Json.Repositories;

public sealed class JsonSigningKeyRepository : ISigningKeyRepository
{
    private readonly JsonDocumentStore<SigningKey> _store;

    public JsonSigningKeyRepository(string dataDirectory)
    {
        _store = new JsonDocumentStore<SigningKey>(Path.Combine(dataDirectory, "signing-keys.json"));
    }

    public async Task<SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken ct)
        => await _store.FindAsync(k => k.KeyId == keyId, ct);

    public async Task<bool> ExistsByKeyIdAsync(string keyId, CancellationToken ct)
        => await _store.AnyAsync(k => k.KeyId == keyId, ct);

    public async Task<IReadOnlyList<SigningKey>> FindByPublisherIdAsync(string publisherId, CancellationToken ct)
        => await _store.WhereAsync(k => k.PublisherId == publisherId, ct);

    public async Task CreateAsync(SigningKey key, CancellationToken ct)
        => await _store.AddAsync(key, ct);

    public async Task UpdateAsync(SigningKey key, CancellationToken ct)
        => await _store.UpdateAsync(k => k.Id == key.Id, key, ct);
}
