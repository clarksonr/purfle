using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purfle.Marketplace.Core.Entities;
using Purfle.Marketplace.Core.Repositories;
using Purfle.Marketplace.Core.Storage;
using Purfle.Marketplace.Storage.Json.BlobStorage;
using Purfle.Marketplace.Storage.Json.Identity;
using Purfle.Marketplace.Storage.Json.OpenIddict;
using Purfle.Marketplace.Storage.Json.Repositories;

namespace Purfle.Marketplace.Storage.Json;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsonStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var dataDirectory = configuration["Storage:DataDirectory"] ?? "./data";
        var manifestStore = configuration["Storage:ManifestStore"] ?? "Local";

        // Ensure data directories exist
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(Path.Combine(dataDirectory, "openiddict"));

        // Repository interfaces
        services.AddSingleton<IPublisherRepository>(new JsonPublisherRepository(dataDirectory));
        services.AddSingleton<ISigningKeyRepository>(new JsonSigningKeyRepository(dataDirectory));
        services.AddSingleton<IAgentListingRepository>(new JsonAgentListingRepository(dataDirectory));
        services.AddSingleton<IAgentVersionRepository>(new JsonAgentVersionRepository(dataDirectory));

        // Blob store
        if (manifestStore == "Azure")
        {
            // Azure blob store will be registered in Step 6
            throw new InvalidOperationException(
                "Azure Blob Storage is not yet configured. Use 'Local' for ManifestStore.");
        }
        else
        {
            services.AddSingleton<IManifestBlobStore>(new LocalFileBlobStore(dataDirectory));
        }

        // Identity stores
        services.AddSingleton<IUserStore<Publisher>>(new JsonUserStore(dataDirectory));
        services.AddSingleton<IRoleStore<IdentityRole>>(new JsonRoleStore(dataDirectory));

        // OpenIddict stores
        services.AddSingleton(new JsonApplicationStore(dataDirectory));
        services.AddSingleton(new JsonAuthorizationStore(dataDirectory));
        services.AddSingleton(new JsonScopeStore(dataDirectory));
        services.AddSingleton(new JsonTokenStore(dataDirectory));

        return services;
    }

    public static OpenIddictCoreBuilder UseJsonStores(this OpenIddictCoreBuilder builder)
    {
        builder.Services.AddSingleton(
            typeof(OpenIddict.Abstractions.IOpenIddictApplicationStore<>).MakeGenericType(typeof(OpenIddictJsonApplication)),
            sp => sp.GetRequiredService<JsonApplicationStore>());

        builder.Services.AddSingleton(
            typeof(OpenIddict.Abstractions.IOpenIddictAuthorizationStore<>).MakeGenericType(typeof(OpenIddictJsonAuthorization)),
            sp => sp.GetRequiredService<JsonAuthorizationStore>());

        builder.Services.AddSingleton(
            typeof(OpenIddict.Abstractions.IOpenIddictScopeStore<>).MakeGenericType(typeof(OpenIddictJsonScope)),
            sp => sp.GetRequiredService<JsonScopeStore>());

        builder.Services.AddSingleton(
            typeof(OpenIddict.Abstractions.IOpenIddictTokenStore<>).MakeGenericType(typeof(OpenIddictJsonToken)),
            sp => sp.GetRequiredService<JsonTokenStore>());

        builder.ReplaceDefaultEntities<OpenIddictJsonApplication, OpenIddictJsonAuthorization, OpenIddictJsonScope, OpenIddictJsonToken, string>();

        return builder;
    }
}
