using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Core.Storage;
using Purfle.Marketplace.Shared;
using Purfle.Runtime.Identity;
using Purfle.Runtime.Manifest;
using CoreEntities = Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Api.Controllers;

[ApiController]
[Route("api/agents")]
public sealed class AgentsController(
    IAgentListingRepository agentListings,
    IAgentVersionRepository agentVersions,
    ISigningKeyRepository signingKeys,
    IPublisherRepository publishers,
    IManifestBlobStore blobStore,
    IKeyRegistry keyRegistry) : ControllerBase
{
    private static readonly ManifestLoader s_manifestLoader = new();

    /// <summary>
    /// Search/list agents.
    /// </summary>
    [HttpGet]
    public async Task<AgentSearchResponse> Search(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var result = await agentListings.SearchAsync(q, page, pageSize, ct);

        var agents = result.Items.Select(a => new AgentSearchResult(
            a.AgentId,
            a.Name,
            a.Description,
            a.LatestVersion ?? "0.0.0",
            a.PublisherDisplayName,
            a.LatestPublishedAt ?? DateTimeOffset.MinValue,
            a.TotalDownloads
        )).ToList();

        return new AgentSearchResponse(agents, page, pageSize, result.TotalCount);
    }

    /// <summary>
    /// Get agent detail with version history.
    /// </summary>
    [HttpGet("{agentId}")]
    public async Task<ActionResult<AgentDetailResponse>> GetDetail(string agentId, CancellationToken ct)
    {
        var listing = await agentListings.FindByAgentIdAsync(agentId, ct);

        if (listing is null || !listing.IsListed)
            return NotFound();

        var publisher = await GetPublisherDisplayName(listing.PublisherId, ct);
        var versions = await agentVersions.FindByListingIdAsync(listing.Id, ct);

        return new AgentDetailResponse(
            listing.AgentId,
            listing.Name,
            listing.Description,
            publisher,
            listing.CreatedAt,
            versions.Select(v => new AgentVersionSummary(
                v.Version,
                v.PublishedAt,
                v.Downloads
            )).ToList()
        );
    }

    /// <summary>
    /// Download a specific version's signed manifest JSON.
    /// </summary>
    [HttpGet("{agentId}/versions/{version}")]
    public async Task<ActionResult> GetVersion(string agentId, string version, CancellationToken ct)
    {
        var agentVersion = await agentVersions.FindByAgentIdAndVersionAsync(agentId, version, ct);

        if (agentVersion is null)
            return NotFound();

        await agentVersions.IncrementDownloadsAsync(agentVersion.Id, ct);

        var manifestJson = await blobStore.RetrieveAsync(agentVersion.ManifestBlobRef, ct);
        return Content(manifestJson, "application/json");
    }

    /// <summary>
    /// Download the latest version's signed manifest JSON.
    /// </summary>
    [HttpGet("{agentId}/latest")]
    public async Task<ActionResult> GetLatest(string agentId, CancellationToken ct)
    {
        var agentVersion = await agentVersions.FindLatestByAgentIdAsync(agentId, ct);

        if (agentVersion is null)
            return NotFound();

        await agentVersions.IncrementDownloadsAsync(agentVersion.Id, ct);

        var manifestJson = await blobStore.RetrieveAsync(agentVersion.ManifestBlobRef, ct);
        return Content(manifestJson, "application/json");
    }

    /// <summary>
    /// Publish a new agent (or new version). Accepts the signed manifest JSON as the request body.
    /// Validates the JWS signature using the runtime's IdentityVerifier.
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Publish(CancellationToken ct)
    {
        var publisherId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (publisherId is null)
            return Unauthorized();

        // Read the raw manifest JSON from the request body.
        string manifestJson;
        using (var reader = new StreamReader(Request.Body))
            manifestJson = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(manifestJson))
            return BadRequest("Request body must contain a signed manifest JSON.");

        // Step 1-2: Parse and schema-validate.
        var parseResult = s_manifestLoader.Load(manifestJson);
        if (!parseResult.Success)
            return BadRequest($"Manifest validation failed: {parseResult.FailureMessage}");

        var manifest = parseResult.Manifest!;

        // Step 3: Verify JWS signature.
        var verifier = new IdentityVerifier(keyRegistry);
        var verifyResult = await verifier.VerifyAsync(manifest, manifestJson, ct);
        if (!verifyResult.Success)
            return BadRequest($"Identity verification failed: {verifyResult.FailureMessage}");

        // Verify the signing key belongs to the authenticated publisher.
        var signingKey = await signingKeys.FindByKeyIdAsync(manifest.Identity.KeyId, ct);

        if (signingKey is null || signingKey.PublisherId != publisherId)
            return BadRequest("The signing key does not belong to your account.");

        // Find or create the agent listing.
        var listing = await agentListings.FindByAgentIdAsync(manifest.Id, ct);

        var now = DateTimeOffset.UtcNow;

        if (listing is null)
        {
            listing = new CoreEntities.AgentListing
            {
                Id = Guid.NewGuid(),
                AgentId = manifest.Id,
                Name = manifest.Name,
                Description = manifest.Description,
                CreatedAt = now,
                UpdatedAt = now,
                IsListed = true,
                PublisherId = publisherId,
            };
            await agentListings.CreateAsync(listing, ct);
        }
        else
        {
            if (listing.PublisherId != publisherId)
                return Forbid();

            listing.Name = manifest.Name;
            listing.Description = manifest.Description;
            listing.UpdatedAt = now;
            await agentListings.UpdateAsync(listing, ct);
        }

        // Check for duplicate version.
        if (await agentVersions.ExistsAsync(listing.Id, manifest.Version, ct))
            return Conflict($"Version {manifest.Version} already exists for agent '{manifest.Id}'.");

        // Store manifest in blob store.
        var blobRef = await blobStore.StoreAsync(manifest.Id, manifest.Version, manifestJson, ct);

        var agentVersion = new CoreEntities.AgentVersion
        {
            Id = Guid.NewGuid(),
            AgentListingId = listing.Id,
            Version = manifest.Version,
            ManifestBlobRef = blobRef,
            SigningKeyId = signingKey.Id,
            PublishedAt = now,
        };

        await agentVersions.CreateAsync(agentVersion, ct);

        return CreatedAtAction(nameof(GetDetail), new { agentId = manifest.Id }, new
        {
            agentId = manifest.Id,
            version = manifest.Version,
            message = "Agent published successfully.",
        });
    }

    /// <summary>
    /// Unlist an agent. Only the owning publisher can unlist.
    /// </summary>
    [HttpDelete("{agentId}")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Unlist(string agentId, CancellationToken ct)
    {
        var publisherId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (publisherId is null)
            return Unauthorized();

        var listing = await agentListings.FindByAgentIdAsync(agentId, ct);

        if (listing is null)
            return NotFound();

        if (listing.PublisherId != publisherId)
            return Forbid();

        listing.IsListed = false;
        listing.UpdatedAt = DateTimeOffset.UtcNow;
        await agentListings.UpdateAsync(listing, ct);

        return NoContent();
    }

    private async Task<string> GetPublisherDisplayName(string publisherId, CancellationToken ct)
    {
        var publisher = await publishers.FindByIdAsync(publisherId, ct);
        return publisher?.DisplayName ?? publisherId;
    }
}
