using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Repositories;

namespace Purfle.Marketplace.Tests;

public sealed class AgentRegistryTests : IDisposable
{
    private readonly string _dataDir;
    private readonly IAgentListingRepository _listings;
    private readonly IAgentVersionRepository _versions;

    public AgentRegistryTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);
        _listings = new JsonAgentListingRepository(_dataDir);
        _versions = new JsonAgentVersionRepository(_dataDir);
    }

    [Fact]
    public async Task CreateAndFindListing()
    {
        var listing = new AgentListing
        {
            Id = Guid.NewGuid(),
            AgentId = "dev.purfle.test-agent",
            PublisherId = "pub-1",
            Name = "Test Agent",
            Description = "A test agent",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _listings.CreateAsync(listing);
        var found = await _listings.FindByAgentIdAsync("dev.purfle.test-agent");

        Assert.NotNull(found);
        Assert.Equal("Test Agent", found.Name);
        Assert.True(found.IsListed);
    }

    [Fact]
    public async Task SearchReturnsMatchingAgents()
    {
        var listing = new AgentListing
        {
            Id = Guid.NewGuid(),
            AgentId = "dev.purfle.file-helper",
            PublisherId = "pub-1",
            Name = "File Helper",
            Description = "Helps with files",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _listings.CreateAsync(listing);
        var result = await _listings.SearchAsync("file", 1, 20);

        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, i => i.AgentId == "dev.purfle.file-helper");
    }

    [Fact]
    public async Task PublishVersionAndDownload()
    {
        var listingId = Guid.NewGuid();
        var listing = new AgentListing
        {
            Id = listingId,
            AgentId = "dev.purfle.versioned",
            PublisherId = "pub-1",
            Name = "Versioned Agent",
            Description = "Has versions",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _listings.CreateAsync(listing);

        var version = new AgentVersion
        {
            Id = Guid.NewGuid(),
            AgentListingId = listingId,
            Version = "1.0.0",
            ManifestBlobRef = "blobs/versioned/1.0.0.json",
            SigningKeyId = Guid.NewGuid(),
            PublishedAt = DateTimeOffset.UtcNow,
        };

        await _versions.CreateAsync(version);

        var latest = await _versions.FindLatestByAgentIdAsync("dev.purfle.versioned");
        Assert.NotNull(latest);
        Assert.Equal("1.0.0", latest.Version);

        await _versions.IncrementDownloadsAsync(latest.Id);
        var updated = await _versions.FindLatestByAgentIdAsync("dev.purfle.versioned");
        Assert.Equal(1, updated!.Downloads);
    }

    [Fact]
    public async Task UnlistAgentHidesFromSearch()
    {
        var listing = new AgentListing
        {
            Id = Guid.NewGuid(),
            AgentId = "dev.purfle.unlisted",
            PublisherId = "pub-1",
            Name = "Soon Unlisted",
            Description = "Will be unlisted",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _listings.CreateAsync(listing);

        listing.IsListed = false;
        await _listings.UpdateAsync(listing);

        var found = await _listings.FindByAgentIdAsync("dev.purfle.unlisted");
        Assert.NotNull(found);
        Assert.False(found.IsListed);
    }

    [Fact]
    public async Task DuplicateVersionDetected()
    {
        var listingId = Guid.NewGuid();
        await _listings.CreateAsync(new AgentListing
        {
            Id = listingId,
            AgentId = "dev.purfle.dup",
            PublisherId = "pub-1",
            Name = "Dup",
            Description = "Duplicate test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await _versions.CreateAsync(new AgentVersion
        {
            Id = Guid.NewGuid(),
            AgentListingId = listingId,
            Version = "1.0.0",
            ManifestBlobRef = "ref",
            SigningKeyId = Guid.NewGuid(),
            PublishedAt = DateTimeOffset.UtcNow,
        });

        var exists = await _versions.ExistsAsync(listingId, "1.0.0");
        Assert.True(exists);

        var notExists = await _versions.ExistsAsync(listingId, "2.0.0");
        Assert.False(notExists);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }
}
