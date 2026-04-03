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
        services.AddSingleton<IAttestationRepository>(new JsonAttestationRepository(dataDirectory));

        // Blob stores
        if (manifestStore == "Azure")
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException("AzureBlobStorage:ConnectionString is required when ManifestStore is 'Azure'.");
            var containerName = configuration["AzureBlobStorage:ContainerName"] ?? "purfle-manifests";
            services.AddSingleton<IManifestBlobStore>(new AzureBlobStore(connectionString, containerName));
        }
        else
        {
            services.AddSingleton<IManifestBlobStore>(new LocalFileBlobStore(dataDirectory));
        }

        // Bundle blob store
        var bundleStore = configuration["Storage:BundleStore"] ?? "Local";
        if (bundleStore == "Azure")
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"]
                ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
                ?? throw new InvalidOperationException("AzureBlobStorage:ConnectionString or AZURE_STORAGE_CONNECTION_STRING is required when BundleStore is 'Azure'.");
            var containerName = configuration["AzureBlobStorage:BundleContainerName"]
                ?? Environment.GetEnvironmentVariable("PURFLE_BUNDLES_CONTAINER")
                ?? "purfle-bundles";
            services.AddSingleton<IBundleBlobStore>(new AzureBlobBundleStore(connectionString, containerName));
        }
        else
        {
            services.AddSingleton<IBundleBlobStore>(new LocalFileBundleStore(dataDirectory));
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
            typeof(global::OpenIddict.Abstractions.IOpenIddictApplicationStore<>).MakeGenericType(typeof(OpenIddictJsonApplication)),
            sp => sp.GetRequiredService<JsonApplicationStore>());

        builder.Services.AddSingleton(
            typeof(global::OpenIddict.Abstractions.IOpenIddictAuthorizationStore<>).MakeGenericType(typeof(OpenIddictJsonAuthorization)),
            sp => sp.GetRequiredService<JsonAuthorizationStore>());

        builder.Services.AddSingleton(
            typeof(global::OpenIddict.Abstractions.IOpenIddictScopeStore<>).MakeGenericType(typeof(OpenIddictJsonScope)),
            sp => sp.GetRequiredService<JsonScopeStore>());

        builder.Services.AddSingleton(
            typeof(global::OpenIddict.Abstractions.IOpenIddictTokenStore<>).MakeGenericType(typeof(OpenIddictJsonToken)),
            sp => sp.GetRequiredService<JsonTokenStore>());

        builder.SetDefaultApplicationEntity<OpenIddictJsonApplication>()
               .SetDefaultAuthorizationEntity<OpenIddictJsonAuthorization>()
               .SetDefaultScopeEntity<OpenIddictJsonScope>()
               .SetDefaultTokenEntity<OpenIddictJsonToken>();

        return builder;
    }
}
