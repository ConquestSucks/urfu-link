using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Application.Handlers.Call;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Discipline;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;
using Urfu.Link.Services.Notification.Infrastructure.Redis;
using Urfu.Link.Services.Notification.Realtime;

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

        // Reuse the Redis multiplexer registered by AddIdempotency above for badge counters.
        services.AddSingleton<IBadgeStore>(sp =>
            new RedisBadgeStore(sp.GetRequiredService<IConnectionMultiplexer>()));

        services.AddScoped<PartitionManager>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Replaced by gRPC-backed UserServiceClient in Wave 13.
        services.AddSingleton<IUserPreferencesClient, StubUserPreferencesClient>();

        services.AddScoped<NotificationFactory>();
        services.AddScoped<NotificationRouter>();
        services.AddScoped<MarkAsReadService>();
        services.AddScoped<BadgeService>();
        services.AddScoped<InAppChannel>();
        services.AddSingleton<INotificationBroadcaster, NotificationBroadcaster>();

        services.AddScoped<ChatMessageSentHandler>();
        services.AddScoped<ChatMentionCreatedHandler>();
        services.AddScoped<AdminChatInviteHandler>();
        services.AddScoped<AdminRoleChangedHandler>();
        services.AddScoped<UserEnrolledHandler>();
        services.AddScoped<EnrollmentRoleChangedHandler>();
        services.AddScoped<DisciplineAnnouncementHandler>();
        services.AddScoped<DisciplineMaterialHandler>();
        services.AddScoped<DisciplineDeadlineHandler>();
        services.AddScoped<CallIncomingHandler>();
        services.AddScoped<CallMissedHandler>();

        return services;
    }
}
