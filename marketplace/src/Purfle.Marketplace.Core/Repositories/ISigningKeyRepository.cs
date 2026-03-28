using Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Core.Repositories;

public interface ISigningKeyRepository
{
    Task<SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken ct = default);
    Task<bool> ExistsByKeyIdAsync(string keyId, CancellationToken ct = default);
    Task<IReadOnlyList<SigningKey>> FindByPublisherIdAsync(string publisherId, CancellationToken ct = default);
    Task CreateAsync(SigningKey key, CancellationToken ct = default);
    Task UpdateAsync(SigningKey key, CancellationToken ct = default);
}
