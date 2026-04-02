using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Purfle.Marketplace.Api.Services;
using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Shared;

namespace Purfle.Marketplace.Api.Controllers;

[ApiController]
[Route("api/publishers")]
public sealed class PublishersController(
    IPublisherRepository publishers,
    UserManager<Publisher> userManager,
    PublisherVerificationService verificationService) : ControllerBase
{
    /// <summary>
    /// Register a new publisher with a domain claim.
    /// Returns a verification challenge.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<VerificationChallengeResponse>> Register(
        [FromBody] RegisterPublisherRequest request,
        CancellationToken ct)
    {
        var existing = await publishers.FindByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Conflict("A publisher with this email already exists.");

        var publisher = new Publisher
        {
            DisplayName = request.DisplayName,
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            UserName = request.Email,
            NormalizedUserName = request.Email.ToUpperInvariant(),
            Domain = request.Domain,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await userManager.CreateAsync(publisher, request.Password);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        var challenge = await verificationService.GenerateChallengeAsync(publisher, request.Domain, ct);

        return Ok(new VerificationChallengeResponse(
            challenge,
            $"Add a file at https://{request.Domain}/.well-known/purfle-verify.txt containing the line: purfle-verify={challenge}"
        ));
    }

    /// <summary>
    /// Verify domain ownership by checking for the verification challenge.
    /// </summary>
    [HttpPost("verify")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<ActionResult> VerifyDomain(
        [FromBody] VerifyDomainRequest request,
        CancellationToken ct)
    {
        var publisherId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (publisherId is null)
            return Unauthorized();

        var publisher = await publishers.FindByIdAsync(publisherId, ct);
        if (publisher is null)
            return NotFound("Publisher not found.");

        if (publisher.Domain != request.Domain)
            return BadRequest("Domain does not match your registered domain.");

        var verified = await verificationService.VerifyDomainAsync(publisher, ct);

        if (!verified)
            return BadRequest("Verification failed. Ensure the file at " +
                $"https://{request.Domain}/.well-known/purfle-verify.txt " +
                $"contains: purfle-verify={publisher.VerificationChallenge}");

        return Ok(new { message = "Domain verified successfully.", domain = request.Domain });
    }

    /// <summary>
    /// Get publisher details.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PublisherDetailResponse>> GetPublisher(string id, CancellationToken ct)
    {
        var publisher = await publishers.FindByIdAsync(id, ct);
        if (publisher is null)
            return NotFound();

        return new PublisherDetailResponse(
            publisher.Id,
            publisher.DisplayName,
            publisher.Domain,
            publisher.IsVerified,
            publisher.CreatedAt
        );
    }
}
