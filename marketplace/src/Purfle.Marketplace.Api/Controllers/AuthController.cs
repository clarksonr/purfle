using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Shared;
using static OpenIddict.Abstractions.OpenIddictConstants;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Purfle.Marketplace.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<Publisher> userManager) : ControllerBase
{
    /// <summary>
    /// Register a new publisher account.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<TokenResponse>> Register(RegisterRequest request)
    {
        var publisher = new Publisher
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await userManager.CreateAsync(publisher, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = errors });
        }

        return Ok(new { message = "Account created. Use /connect/authorize to obtain tokens." });
    }
}

/// <summary>
/// OpenIddict authorization and token endpoints.
/// </summary>
[ApiController]
public sealed class OidcController(
    UserManager<Publisher> userManager,
    SignInManager<Publisher> signInManager) : ControllerBase
{
    /// <summary>
    /// OAuth2 Authorization endpoint — handles the PKCE authorization code flow.
    /// For CLI/MAUI: opens in system browser, user logs in, redirect back with code.
    /// </summary>
    [HttpGet("connect/authorize")]
    [HttpPost("connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Check if the user is authenticated.
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (!result.Succeeded)
        {
            // Redirect to login page, preserving the return URL.
            var properties = new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                    Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
            };

            return Challenge(properties, IdentityConstants.ApplicationScheme);
        }

        var user = await userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.AddClaim(Claims.Subject, user.Id);
        identity.AddClaim(Claims.Name, user.DisplayName);
        identity.AddClaim(Claims.Email, user.Email!);

        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(static claim => claim.Type switch
        {
            Claims.Name or Claims.Email => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        });

        return SignIn(new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// OAuth2 Token endpoint — exchanges authorization codes for tokens.
    /// </summary>
    [HttpPost("connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            var user = await userManager.FindByEmailAsync(request.Username!);
            if (user is null)
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var identity = new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                Claims.Name,
                Claims.Role);

            identity.AddClaim(Claims.Subject, user.Id);
            identity.AddClaim(Claims.Name, user.DisplayName);
            identity.AddClaim(Claims.Email, user.Email!);

            identity.SetDestinations(static claim => claim.Type switch
            {
                Claims.Name or Claims.Email => [Destinations.AccessToken, Destinations.IdentityToken],
                _ => [Destinations.AccessToken],
            });

            return SignIn(new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var userId = result.Principal?.GetClaim(Claims.Subject);
            var user = userId is not null ? await userManager.FindByIdAsync(userId) : null;

            if (user is null)
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var identity = new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                Claims.Name,
                Claims.Role);

            identity.AddClaim(Claims.Subject, user.Id);
            identity.AddClaim(Claims.Name, user.DisplayName);
            identity.AddClaim(Claims.Email, user.Email!);

            identity.SetDestinations(static claim => claim.Type switch
            {
                Claims.Name or Claims.Email => [Destinations.AccessToken, Destinations.IdentityToken],
                _ => [Destinations.AccessToken],
            });

            return SignIn(new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new { error = "unsupported_grant_type" });
    }
}
