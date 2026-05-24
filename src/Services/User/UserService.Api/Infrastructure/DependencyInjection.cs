using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using UserService.Api.Application;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Devices;
using UserService.Api.Infrastructure.Keycloak;
using UserService.Api.Infrastructure.Persistence;
using UserService.Api.Infrastructure.Search;
using UserService.Api.Services;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Infrastructure.Storage;

namespace UserService.Api.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddUserModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new ServiceProfile(
            "user-service",
            "postgresql",
            KafkaTopicNames.UserEvents,
            "user.sample.v1"));

        services.AddScoped<SampleEventDispatcher>();

        services.AddDbContextPool<UserDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Primary")));

        services.AddScoped<IUserRepository, UserRepository>();

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
        services.AddSingleton<IAvatarStorage, MinioAvatarStorage>();

        services.Configure<KeycloakAdminOptions>(configuration.GetSection(KeycloakAdminOptions.SectionName));
        services.AddHttpClient<ISessionManager, KeycloakSessionClient>();
        services.AddHttpClient<IUserDirectory, KeycloakUserClient>();

        services.AddSingleton<IDeviceRegistry, RedisDeviceRegistry>();
        services.AddSessionRevocation(configuration);

        // User search infrastructure: транслитератор + builder — singleton (stateless),
        // репозиторий — scoped (использует UserDbContext).
        services.AddSingleton<Transliterator>();
        services.AddSingleton<UserSearchTextBuilder>();
        services.AddScoped<IUserSearchRepository, PgUserSearchRepository>();

        services.Configure<UserSearchReconcilerOptions>(
            configuration.GetSection(UserSearchReconcilerOptions.SectionName));
        services.Configure<UserSearchLazyUpserterOptions>(
            configuration.GetSection(UserSearchLazyUpserterOptions.SectionName));

        services.AddSingleton<UserSearchLazyUpserter>();
        services.AddHostedService<UserSearchReconciler>();

        return services;
    }
}
