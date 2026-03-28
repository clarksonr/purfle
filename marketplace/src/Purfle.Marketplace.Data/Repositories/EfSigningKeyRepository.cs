using Microsoft.EntityFrameworkCore;
using Purfle.Marketplace.Core.Repositories;
using CoreEntities = Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Data.Repositories;

public sealed class EfSigningKeyRepository(MarketplaceDbContext db) : ISigningKeyRepository
{
    public async Task<CoreEntities.SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken ct)
    {
        var key = await db.SigningKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyId == keyId, ct);
        return key is null ? null : ToCore(key);
    }

    public async Task<bool> ExistsByKeyIdAsync(string keyId, CancellationToken ct)
    {
        return await db.SigningKeys.AnyAsync(k => k.KeyId == keyId, ct);
    }

    public async Task<IReadOnlyList<CoreEntities.SigningKey>> FindByPublisherIdAsync(string publisherId, CancellationToken ct)
    {
        var keys = await db.SigningKeys.AsNoTracking()
            .Where(k => k.PublisherId == publisherId)
            .ToListAsync(ct);
        return keys.Select(ToCore).ToList();
    }

    public async Task CreateAsync(CoreEntities.SigningKey key, CancellationToken ct)
    {
        db.SigningKeys.Add(ToEf(key));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CoreEntities.SigningKey key, CancellationToken ct)
    {
        var existing = await db.SigningKeys.FirstOrDefaultAsync(k => k.Id == key.Id, ct)
            ?? throw new InvalidOperationException($"SigningKey {key.Id} not found.");
        existing.IsRevoked = key.IsRevoked;
        existing.RevokedAt = key.RevokedAt;
        await db.SaveChangesAsync(ct);
    }

    private static CoreEntities.SigningKey ToCore(Entities.SigningKey e) => new()
    {
        Id = e.Id,
        KeyId = e.KeyId,
        PublisherId = e.PublisherId,
        Algorithm = e.Algorithm,
        PublicKeyX = e.PublicKeyX,
        PublicKeyY = e.PublicKeyY,
        IsRevoked = e.IsRevoked,
        RevokedAt = e.RevokedAt,
        CreatedAt = e.CreatedAt,
    };

    private static Entities.SigningKey ToEf(CoreEntities.SigningKey c) => new()
    {
        Id = c.Id,
        KeyId = c.KeyId,
        PublisherId = c.PublisherId,
        Algorithm = c.Algorithm,
        PublicKeyX = c.PublicKeyX,
        PublicKeyY = c.PublicKeyY,
        IsRevoked = c.IsRevoked,
        RevokedAt = c.RevokedAt,
        CreatedAt = c.CreatedAt,
    };
}
