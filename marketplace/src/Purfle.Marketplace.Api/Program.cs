using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Purfle.Marketplace.Api.Services;
using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Storage.Json;
using Purfle.Runtime.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();

// Storage — JSON file-backed repositories, identity stores, and OpenIddict stores.
builder.Services.AddJsonStorage(builder.Configuration);

// Key registry — bridges the signing key repository to the runtime identity verifier.
builder.Services.AddScoped<IKeyRegistry, DbKeyRegistry>();

// ASP.NET Identity — user/role stores already registered by AddJsonStorage.
builder.Services.AddIdentity<Publisher, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddDefaultTokenProviders();

// Configure cookie for Identity login page (used during OAuth flow).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
});

// OpenIddict — OAuth2/OIDC server backed by JSON stores.
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseJsonStores();
    })
    .AddServer(options =>
    {
        // Enable the authorization code flow with PKCE (for CLI + MAUI).
        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();

        // Allow password grant for local dev tooling (seed scripts, CI).
        options.AllowPasswordFlow();

        // Also allow refresh token flow.
        options.AllowRefreshTokenFlow();

        // Endpoints
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token");

        // Register scopes.
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile);

        // Use development encryption/signing keys (replace in production).
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // Register ASP.NET Core host.
        // DisableTransportSecurityRequirement allows HTTP in development.
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .DisableTransportSecurityRequirement();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// CORS for localhost development.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Seed the OpenIddict application for CLI/MAUI clients on first run.
using (var scope = app.Services.CreateScope())
{
    var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    if (await appManager.FindByClientIdAsync("purfle-cli") is null)
    {
        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "purfle-cli",
            DisplayName = "Purfle CLI / Desktop",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            RedirectUris =
            {
                new Uri("http://localhost:9876/callback"),
                new Uri("purfle://callback"),
            },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
            },
        });
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.Run();
