using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;

namespace Urfu.Link.Services.Notification.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new ServiceProfile(
            "notification-service",
            "postgresql",
            KafkaTopicNames.NotificationEvents,
            "notification.created.v1"));

        services.AddDbContextPool<NotificationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Primary")));

        services.AddIdempotency(configuration);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<PartitionManager>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Replaced by RedisBadgeStore in Wave 6.
        services.AddSingleton<IBadgeStore, InMemoryBadgeStore>();

        // Replaced by gRPC-backed UserServiceClient in Wave 13.
        services.AddSingleton<IUserPreferencesClient, StubUserPreferencesClient>();

        services.AddScoped<NotificationFactory>();
        services.AddScoped<NotificationRouter>();

        services.AddScoped<ChatMessageSentHandler>();

        return services;
    }
}
