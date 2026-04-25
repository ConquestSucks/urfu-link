using Amazon.S3;
using MediaService.Api.Application.Access;
using MediaService.Api.Application.Limits;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.Api.Infrastructure.Persistence.Repositories;
using MediaService.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Outbox;

namespace MediaService.Api.Infrastructure;

public static class ModuleRegistration
{
    public const string ServiceName = "media-service";

    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Persistence
        services.AddDbContextPool<MediaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Primary")));

        services.AddScoped<IMediaAssetRepository>(sp => new MediaAssetRepository(
            sp.GetRequiredService<MediaDbContext>(),
            sp.GetRequiredService<IOutboxWriter>(),
            ServiceName));
        services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();
        services.AddScoped<IMediaAccessGrantRepository, MediaAccessGrantRepository>();

        // Object storage
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                ?? new StorageOptions();
            var config = new AmazonS3Config
            {
                ServiceURL = storageOptions.Endpoint,
                ForcePathStyle = true,
            };
            return new AmazonS3Client(storageOptions.AccessKey, storageOptions.SecretKey, config);
        });
        services.AddSingleton<IMediaObjectStorage, MinioObjectStorage>();
        services.AddSingleton<IPresignedUrlGenerator, PresignedUrlGenerator>();

        // Limits / policy
        services.Configure<MediaLimitsOptions>(configuration.GetSection(MediaLimitsOptions.SectionName));
        services.AddScoped<AccessPolicy>();

        return services;
    }
}
