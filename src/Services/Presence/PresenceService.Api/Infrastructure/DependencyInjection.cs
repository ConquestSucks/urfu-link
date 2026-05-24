using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Application.Dispatchers;
using Urfu.Link.Services.Presence.Application.Sessions;
using Urfu.Link.Services.Presence.Domain;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;
using Urfu.Link.Services.Presence.Infrastructure.Persistence.Repositories;
using Urfu.Link.Services.Presence.Infrastructure.Redis;
using Urfu.Link.Services.Presence.Messaging;
using Urfu.Link.Services.Presence.Realtime;
using Urfu.Link.Services.Presence.Workers;

namespace Urfu.Link.Services.Presence.Infrastructure;

public static class ModuleRegistration
{
    public const string ServiceName = "presence-service";

    public static IServiceCollection AddPresenceModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new ServiceProfile(
            ServiceName,
            "redis+postgres",
            KafkaTopicNames.PresenceEvents,
            "presence.user.online.v1"));

        services.AddOptions<PresenceOptions>()
            .Bind(configuration.GetSection(PresenceOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContextPool<PresenceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Primary")));

        services.AddScoped<ILastSeenRepository>(sp => new LastSeenRepository(
            sp.GetRequiredService<PresenceDbContext>(),
            sp.GetRequiredService<IOutboxWriter>(),
            ServiceName));

        services.AddScoped<PartitionManager>();

        services.AddSingleton<IPresenceSessionStore, RedisPresenceSessionStore>();
        services.AddSingleton<ITypingStore, RedisTypingStore>();
        services.AddSingleton<IPrivacyProjectionStore, RedisPrivacyProjectionStore>();

        services.AddScoped<IKafkaMessageHandler, PrivacyChangedHandler>();

        services.AddSingleton<PresenceAggregator>();
        services.AddScoped<PresenceEventDispatcher>();
        services.AddScoped<DisconnectPresenceSessionService>();
        services.AddSingleton<PresenceBroadcaster>();

        services.AddHostedService<PresenceSweeperWorker>();

        return services;
    }
}


