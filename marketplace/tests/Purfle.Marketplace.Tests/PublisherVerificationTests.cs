using Purfle.Marketplace.Api.Services;
using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Repositories;

namespace Purfle.Marketplace.Tests;

public sealed class PublisherVerificationTests : IDisposable
{
    private readonly string _dataDir;
    private readonly IPublisherRepository _publishers;
    private readonly PublisherVerificationService _service;

    public PublisherVerificationTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);
        _publishers = new JsonPublisherRepository(_dataDir);
        _service = new PublisherVerificationService(_publishers);
    }

    [Fact]
    public async Task GenerateChallengeSetsDomainAndChallenge()
    {
        var publisher = new Publisher
        {
            DisplayName = "Test Publisher",
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _publishers.CreateAsync(publisher);

        var challenge = await _service.GenerateChallengeAsync(publisher, "example.com", CancellationToken.None);

        Assert.NotEmpty(challenge);
        Assert.Equal("example.com", publisher.Domain);
        Assert.Equal(challenge, publisher.VerificationChallenge);

        // Verify persisted
        var reloaded = await _publishers.FindByIdAsync(publisher.Id);
        Assert.Equal("example.com", reloaded!.Domain);
        Assert.Equal(challenge, reloaded.VerificationChallenge);
    }

    [Fact]
    public async Task VerifyDomainFailsWithoutChallenge()
    {
        var publisher = new Publisher
        {
            DisplayName = "No Challenge",
            Email = "no@challenge.com",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _publishers.CreateAsync(publisher);

        var result = await _service.VerifyDomainAsync(publisher, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyDomainFailsForUnreachableDomain()
    {
        var publisher = new Publisher
        {
            DisplayName = "Unreachable",
            Email = "un@reachable.test",
            Domain = "nonexistent-domain-xyz-123456.invalid",
            VerificationChallenge = "test-challenge",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _publishers.CreateAsync(publisher);

        var result = await _service.VerifyDomainAsync(publisher, CancellationToken.None);
        Assert.False(result);
        Assert.False(publisher.IsVerified);
    }

    [Fact]
    public async Task ChallengeIsHexString()
    {
        var publisher = new Publisher
        {
            DisplayName = "Hex Test",
            Email = "hex@test.com",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _publishers.CreateAsync(publisher);

        var challenge = await _service.GenerateChallengeAsync(publisher, "hex.test", CancellationToken.None);

        // Challenge should be 32 hex chars (16 bytes)
        Assert.Equal(32, challenge.Length);
        Assert.True(challenge.All(c => "0123456789abcdef".Contains(c)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }
}
