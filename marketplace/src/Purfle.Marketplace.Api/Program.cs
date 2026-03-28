using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Purfle.Marketplace.Api.Services;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Core.Storage;
using Purfle.Marketplace.Data;
using Purfle.Marketplace.Data.Entities;
using Purfle.Marketplace.Data.Repositories;
using Purfle.Runtime.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();

// Repository interfaces backed by EF Core (transitional)
builder.Services.AddScoped<IPublisherRepository, EfPublisherRepository>();
builder.Services.AddScoped<ISigningKeyRepository, EfSigningKeyRepository>();
builder.Services.AddScoped<IAgentListingRepository, EfAgentListingRepository>();
builder.Services.AddScoped<IAgentVersionRepository, EfAgentVersionRepository>();
builder.Services.AddSingleton<IManifestBlobStore, EfManifestBlobStore>();
builder.Services.AddScoped<IKeyRegistry, DbKeyRegistry>();

// EF Core + SQLite
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("Marketplace")
        ?? "Data Source=marketplace.db");
    options.UseOpenIddict();
});

// ASP.NET Identity
builder.Services.AddIdentity<Publisher, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<MarketplaceDbContext>()
.AddDefaultTokenProviders();

// Configure cookie for Identity login page (used during OAuth flow).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
});

// OpenIddict — OAuth2/OIDC server
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<MarketplaceDbContext>();
    })
    .AddServer(options =>
    {
        // Enable the authorization code flow with PKCE (for CLI + MAUI).
        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();

        // Also allow refresh token flow.
        options.AllowRefreshTokenFlow();

        // Endpoints
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token");

        // Use development encryption/signing keys (replace in production).
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // Register ASP.NET Core host.
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// CORS for localhost development
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

// Auto-create database on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    db.Database.EnsureCreated();

    // Seed the OpenIddict application for CLI/MAUI clients.
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
