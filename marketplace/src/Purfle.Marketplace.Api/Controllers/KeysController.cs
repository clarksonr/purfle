using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Shared;
using CoreEntities = Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Api.Controllers;

[ApiController]
[Route("api/keys")]
public sealed class KeysController(ISigningKeyRepository signingKeys) : ControllerBase
{
    /// <summary>
    /// Get a public key by key_id. Public endpoint — target for HttpKeyRegistryClient.
    /// </summary>
    [HttpGet("{keyId}")]
    public async Task<ActionResult<PublicKeyResponse>> Get(string keyId, CancellationToken ct)
    {
        var key = await signingKeys.FindByKeyIdAsync(keyId, ct);

        if (key is null)
            return NotFound();

        return new PublicKeyResponse(
            key.KeyId,
            key.Algorithm,
            Convert.ToBase64String(key.PublicKeyX),
            Convert.ToBase64String(key.PublicKeyY),
            key.IsRevoked
        );
    }

    /// <summary>
    /// Register a new signing key. Requires authentication.
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<ActionResult<PublicKeyResponse>> Register(
        RegisterKeyRequest request, CancellationToken ct)
    {
        var publisherId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (publisherId is null)
            return Unauthorized();

        if (await signingKeys.ExistsByKeyIdAsync(request.KeyId, ct))
            return Conflict($"Key '{request.KeyId}' is already registered.");

        if (request.Algorithm != "ES256")
            return BadRequest("Only ES256 is supported in v0.1.");

        byte[] x, y;
        try
        {
            x = Convert.FromBase64String(request.X);
            y = Convert.FromBase64String(request.Y);
        }
        catch (FormatException)
        {
            return BadRequest("X and Y must be valid Base64-encoded P-256 coordinates.");
        }

        if (x.Length != 32 || y.Length != 32)
            return BadRequest("X and Y must each be 32 bytes (P-256).");

        var key = new CoreEntities.SigningKey
        {
            Id = Guid.NewGuid(),
            KeyId = request.KeyId,
            Algorithm = request.Algorithm,
            PublicKeyX = x,
            PublicKeyY = y,
            CreatedAt = DateTimeOffset.UtcNow,
            PublisherId = publisherId,
        };

        await signingKeys.CreateAsync(key, ct);

        return CreatedAtAction(nameof(Get), new { keyId = key.KeyId }, new PublicKeyResponse(
            key.KeyId,
            key.Algorithm,
            request.X,
            request.Y,
            key.IsRevoked
        ));
    }

    /// <summary>
    /// Revoke a signing key. Only the owning publisher can revoke.
    /// </summary>
    [HttpDelete("{keyId}")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Revoke(string keyId, CancellationToken ct)
    {
        var publisherId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (publisherId is null)
            return Unauthorized();

        var key = await signingKeys.FindByKeyIdAsync(keyId, ct);

        if (key is null)
            return NotFound();

        if (key.PublisherId != publisherId)
            return Forbid();

        if (key.IsRevoked)
            return NoContent();

        key.IsRevoked = true;
        key.RevokedAt = DateTimeOffset.UtcNow;
        await signingKeys.UpdateAsync(key, ct);

        return NoContent();
    }
}
