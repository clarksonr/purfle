using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Purfle.Marketplace.Data.Entities;

namespace Purfle.Marketplace.Data;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
    : IdentityDbContext<Publisher>(options)
{
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();
    public DbSet<AgentListing> AgentListings => Set<AgentListing>();
    public DbSet<AgentVersion> AgentVersions => Set<AgentVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Publisher>(e =>
        {
            e.Property(p => p.DisplayName).HasMaxLength(200);
        });

        modelBuilder.Entity<SigningKey>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.KeyId).IsUnique();
            e.Property(k => k.KeyId).HasMaxLength(200);
            e.Property(k => k.Algorithm).HasMaxLength(10);
            e.Property(k => k.PublicKeyX).HasMaxLength(32);
            e.Property(k => k.PublicKeyY).HasMaxLength(32);
            e.HasOne(k => k.Publisher)
                .WithMany(p => p.SigningKeys)
                .HasForeignKey(k => k.PublisherId);
        });

        modelBuilder.Entity<AgentListing>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AgentId).IsUnique();
            e.Property(a => a.AgentId).HasMaxLength(100);
            e.Property(a => a.Name).HasMaxLength(200);
            e.Property(a => a.Description).HasMaxLength(2000);
            e.HasOne(a => a.Publisher)
                .WithMany(p => p.AgentListings)
                .HasForeignKey(a => a.PublisherId);
        });

        modelBuilder.Entity<AgentVersion>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.AgentListingId, v.Version }).IsUnique();
            e.Property(v => v.Version).HasMaxLength(50);
            e.HasOne(v => v.AgentListing)
                .WithMany(a => a.Versions)
                .HasForeignKey(v => v.AgentListingId);
            e.HasOne(v => v.SigningKey)
                .WithMany()
                .HasForeignKey(v => v.SigningKeyId);
        });
    }
}
