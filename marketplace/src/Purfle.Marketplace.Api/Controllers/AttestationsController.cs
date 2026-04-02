using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Purfle.Marketplace.Api.Services;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Shared;

namespace Purfle.Marketplace.Api.Controllers;

[ApiController]
[Route("api/attestations")]
public sealed class AttestationsController(
    AttestationService attestationService,
    IAgentListingRepository agentListings,
    IPublisherRepository publishers) : ControllerBase
{
    /// <summary>
    /// Request an attestation for an agent. Only the owning publisher can request.
    /// </summary>
    [HttpPost("request")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<ActionResult> RequestAttestation(
        [FromBody] RequestAttestationRequest request,
        CancellationToken ct)
    {
        var publisherId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (publisherId is null)
            return Unauthorized();

        var listing = await agentListings.FindByAgentIdAsync(request.AgentId, ct);
        if (listing is null)
            return NotFound("Agent not found.");

        if (listing.PublisherId != publisherId)
            return Forbid();

        var publisher = await publishers.FindByIdAsync(publisherId, ct);
        if (publisher is null)
            return NotFound("Publisher not found.");

        if (request.Type == AttestationService.PublisherVerified && !publisher.IsVerified)
            return BadRequest("Publisher must be verified to request publisher-verified attestation.");

        await attestationService.IssuePublishAttestationsAsync(
            request.AgentId,
            publisher.IsVerified,
            ct);

        return Ok(new { message = "Attestation issued." });
    }

    /// <summary>
    /// Get all attestations for an agent.
    /// </summary>
    [HttpGet("{agentId}")]
    public async Task<ActionResult<IReadOnlyList<AttestationResponse>>> GetAttestations(
        string agentId,
        CancellationToken ct)
    {
        var attestations = await attestationService.GetAttestationsAsync(agentId, ct);

        var result = attestations.Select(a => new AttestationResponse(
            a.Id,
            a.AgentId,
            a.Type,
            a.IssuedBy,
            a.IssuedAt
        )).ToList();

        return Ok(result);
    }
}
