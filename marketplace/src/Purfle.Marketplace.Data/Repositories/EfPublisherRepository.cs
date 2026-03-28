using Microsoft.EntityFrameworkCore;
using Purfle.Marketplace.Core.Repositories;
using CoreEntities = Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Data.Repositories;

public sealed class EfPublisherRepository(MarketplaceDbContext db) : IPublisherRepository
{
    public async Task<CoreEntities.Publisher?> FindByIdAsync(string id, CancellationToken ct)
    {
        var p = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return p is null ? null : ToCore(p);
    }

    public async Task<CoreEntities.Publisher?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.ToUpperInvariant();
        var p = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        return p is null ? null : ToCore(p);
    }

    public async Task<CoreEntities.Publisher?> FindByNameAsync(string userName, CancellationToken ct)
    {
        var normalized = userName.ToUpperInvariant();
        var p = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
        return p is null ? null : ToCore(p);
    }

    public async Task CreateAsync(CoreEntities.Publisher publisher, CancellationToken ct)
    {
        db.Users.Add(ToEf(publisher));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CoreEntities.Publisher publisher, CancellationToken ct)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == publisher.Id, ct)
            ?? throw new InvalidOperationException($"Publisher {publisher.Id} not found.");
        existing.DisplayName = publisher.DisplayName;
        existing.IsVerified = publisher.IsVerified;
        existing.Email = publisher.Email;
        existing.NormalizedEmail = publisher.NormalizedEmail;
        existing.UserName = publisher.UserName;
        existing.NormalizedUserName = publisher.NormalizedUserName;
        existing.PasswordHash = publisher.PasswordHash;
        existing.SecurityStamp = publisher.SecurityStamp;
        existing.ConcurrencyStamp = publisher.ConcurrencyStamp;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CoreEntities.Publisher publisher, CancellationToken ct)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == publisher.Id, ct);
        if (existing is not null)
        {
            db.Users.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }

    private static CoreEntities.Publisher ToCore(Entities.Publisher e) => new()
    {
        Id = e.Id,
        Email = e.Email,
        NormalizedEmail = e.NormalizedEmail,
        UserName = e.UserName,
        NormalizedUserName = e.NormalizedUserName,
        PasswordHash = e.PasswordHash,
        DisplayName = e.DisplayName,
        CreatedAt = e.CreatedAt,
        IsVerified = e.IsVerified,
        SecurityStamp = e.SecurityStamp,
        ConcurrencyStamp = e.ConcurrencyStamp,
    };

    private static Entities.Publisher ToEf(CoreEntities.Publisher c) => new()
    {
        Id = c.Id,
        Email = c.Email,
        NormalizedEmail = c.NormalizedEmail,
        UserName = c.UserName,
        NormalizedUserName = c.NormalizedUserName,
        PasswordHash = c.PasswordHash,
        DisplayName = c.DisplayName,
        CreatedAt = c.CreatedAt,
        IsVerified = c.IsVerified,
        SecurityStamp = c.SecurityStamp,
        ConcurrencyStamp = c.ConcurrencyStamp,
    };
}
