using Purfle.Marketplace.Api.Services;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Storage.Json.Repositories;

namespace Purfle.Marketplace.Tests;

public sealed class AttestationTests : IDisposable
{
    private readonly string _dataDir;
    private readonly IAttestationRepository _attestations;
    private readonly AttestationService _service;

    public AttestationTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"purfle-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);
        _attestations = new JsonAttestationRepository(_dataDir);
        _service = new AttestationService(_attestations);
    }

    [Fact]
    public async Task MarketplaceListedIssuedOnPublish()
    {
        await _service.IssuePublishAttestationsAsync("agent-1", publisherIsVerified: false, CancellationToken.None);

        var attestations = await _service.GetAttestationsAsync("agent-1", CancellationToken.None);
        Assert.Single(attestations);
        Assert.Equal(AttestationService.MarketplaceListed, attestations[0].Type);
        Assert.Equal("purfle-marketplace", attestations[0].IssuedBy);
    }

    [Fact]
    public async Task VerifiedPublisherGetsBothAttestations()
    {
        await _service.IssuePublishAttestationsAsync("agent-2", publisherIsVerified: true, CancellationToken.None);

        var attestations = await _service.GetAttestationsAsync("agent-2", CancellationToken.None);
        Assert.Equal(2, attestations.Count);
        Assert.Contains(attestations, a => a.Type == AttestationService.MarketplaceListed);
        Assert.Contains(attestations, a => a.Type == AttestationService.PublisherVerified);
    }

    [Fact]
    public async Task DuplicateAttestationsNotIssued()
    {
        await _service.IssuePublishAttestationsAsync("agent-3", publisherIsVerified: true, CancellationToken.None);
        await _service.IssuePublishAttestationsAsync("agent-3", publisherIsVerified: true, CancellationToken.None);

        var attestations = await _service.GetAttestationsAsync("agent-3", CancellationToken.None);
        Assert.Equal(2, attestations.Count);  // Still just 2, not 4
    }

    [Fact]
    public async Task NoAttestationsForUnknownAgent()
    {
        var attestations = await _service.GetAttestationsAsync("nonexistent", CancellationToken.None);
        Assert.Empty(attestations);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }
}
