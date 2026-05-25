using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Notification.Application.Direct;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Application.Handlers.Call;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Discipline;
using Urfu.Link.Services.Notification.Application.Handlers.Media;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Channels.EmailChannel;
using Urfu.Link.Services.Notification.Channels.PushChannel;
using Urfu.Link.Services.Notification.Channels.PushChannel.Apns;
using Urfu.Link.Services.Notification.Channels.PushChannel.Fcm;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Outbox;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Infrastructure.Grpc;
using Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;
using Urfu.Link.Services.Notification.Infrastructure.Redis;
using Urfu.Link.Services.Notification.Messaging;
using Urfu.Link.Services.Notification.Realtime;
using Urfu.Link.Services.Notification.Workers;
using Urfu.Link.Services.User.Grpc;
using PresenceGrpc = Urfu.Link.Services.Presence.Grpc;

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
        services.AddScoped<IPushDeviceRepository, PushDeviceRepository>();

        services.Configure<UserServiceClientOptions>(configuration.GetSection(UserServiceClientOptions.SectionName));
        // Named because PresenceService also exposes InternalApi.InternalApiClient under
        // a different namespace and the gRPC factory dedupes by simple type name.
        services.AddGrpcClient<InternalApi.InternalApiClient>("user-service", (sp, opts) =>
        {
            var userOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UserServiceClientOptions>>().Value;
            opts.Address = new Uri(userOpts.GrpcEndpoint);
        });
        services.AddSingleton<IUserPreferencesClient, UserServiceClient>();

        // PresenceService gRPC client. When PresenceService:GrpcEndpoint is configured
        // (production, dev compose), use the real client so the router can skip Push
        // when the user is online on web. Otherwise fall back to OfflinePresenceClient
        // (tests, on-prem profile without presence) so push is delivered for everyone —
        // duplicate Push for online users is preferable to no Push for offline users.
        services.Configure<PresenceServiceClientOptions>(
            configuration.GetSection(PresenceServiceClientOptions.SectionName));
        var presenceEndpoint = configuration[$"{PresenceServiceClientOptions.SectionName}:GrpcEndpoint"];
        if (!string.IsNullOrWhiteSpace(presenceEndpoint))
        {
            services.AddGrpcClient<PresenceGrpc.InternalApi.InternalApiClient>("presence-service", (sp, opts) =>
            {
                var presenceOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PresenceServiceClientOptions>>().Value;
                opts.Address = new Uri(presenceOpts.GrpcEndpoint);
            });
            services.AddSingleton<IPresenceClient, GrpcPresenceClient>();
        }
        else
        {
            services.AddSingleton<IPresenceClient, OfflinePresenceClient>();
        }

        services.AddScoped<NotificationFactory>();
        services.AddScoped<NotificationRouter>();
        services.AddScoped<NotificationKafkaEventDispatcher>();
        services.AddScoped<NotificationLifecycleService>();
        services.AddScoped<MarkAsReadService>();
        services.AddScoped<BadgeService>();
        services.AddScoped<InAppChannel>();
        services.AddSingleton<INotificationBroadcaster, NotificationBroadcaster>();

        services.AddScoped<IDisciplineConversationLookup, Infrastructure.Persistence.Repositories.DisciplineConversationLookup>();
        services.AddScoped<ChatMessageSentHandler>();
        services.AddScoped<ChatMentionCreatedHandler>();
        services.AddScoped<ChatThreadReplyPostedHandler>();
        services.AddScoped<ChatReactionAddedHandler>();
        services.AddScoped<ChatMessagePinnedHandler>();
        services.AddScoped<ChatParticipantRoleChangedHandler>();
        services.AddScoped<ChatDisciplineConversationCreatedHandler>();
        services.AddScoped<AdminChatInviteHandler>();
        services.AddScoped<AdminRoleChangedHandler>();
        services.AddScoped<UserEnrolledHandler>();
        services.AddScoped<UserUnenrolledHandler>();
        services.AddScoped<EnrollmentRoleChangedHandler>();
        services.AddScoped<DisciplineAnnouncementHandler>();
        services.AddScoped<DisciplineMaterialHandler>();
        services.AddScoped<DisciplineDeadlineHandler>();
        services.AddScoped<CallIncomingHandler>();
        services.AddScoped<CallMissedHandler>();
        services.AddScoped<MediaAccessGrantedHandler>();
        services.AddScoped<MediaAccessRevokedHandler>();
        services.AddScoped<MediaAssetUploadedHandler>();
        services.AddScoped<MediaAssetDeletedHandler>();
        services.AddScoped<UserDeletedHandler>();
        services.AddScoped<DirectNotificationHandler>();
        services.AddScoped<Urfu.Link.Services.Notification.Application.Handlers.System.SystemMaintenanceHandler>();
        services.AddScoped<Urfu.Link.Services.Notification.Application.Handlers.System.SystemUpdateHandler>();

        services.Configure<FcmOptions>(configuration.GetSection(FcmOptions.SectionName));
        services.Configure<ApnsOptions>(configuration.GetSection(ApnsOptions.SectionName));
        services.Configure<PushDispatcherOptions>(configuration.GetSection(PushDispatcherOptions.SectionName));

        var pushProvider = configuration.GetValue("Notification:Push:Provider", "real")
            ?? "real";

        if (string.Equals(pushProvider, "fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFcmClient, FakeFcmClient>();
            services.AddSingleton<IApnsClient, FakeApnsClient>();
        }
        else
        {
            services.AddSingleton<IFcmClient, FcmClient>();
            services.AddHttpClient("apns");
            services.AddSingleton<IApnsClient, ApnsClient>();
        }

        services.AddScoped<PushDispatcher>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<EmailDispatcherOptions>(configuration.GetSection(EmailDispatcherOptions.SectionName));

        services.AddSingleton<ITemplateRenderer, EmailTemplateRenderer>();

        var smtpProvider = configuration.GetValue("Notification:Smtp:Provider", "real") ?? "real";
        if (string.Equals(smtpProvider, "fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, FakeEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        services.AddScoped<EmailChannel>();

        services.Configure<NotificationOutboxOptions>(configuration.GetSection(NotificationOutboxOptions.SectionName));
        services.Configure<RetentionOptions>(configuration.GetSection(RetentionOptions.SectionName));

        services.AddScoped<IOutboxEnqueue, EfOutboxEnqueue>();

        return services;
    }
}
