using Amazon.S3;
using MediaService.Api.Application.Access;
using MediaService.Api.Application.Limits;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.Api.Infrastructure.Persistence.Repositories;
using MediaService.Api.Infrastructure.Storage;
using MediaService.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var storageOptions = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
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

        // Background workers
        services.Configure<RetentionWorkerOptions>(configuration.GetSection(RetentionWorkerOptions.SectionName));
        services.AddHostedService<UploadSessionCleanupWorker>();
        services.AddHostedService<RetentionWorker>();

        return services;
    }
}
