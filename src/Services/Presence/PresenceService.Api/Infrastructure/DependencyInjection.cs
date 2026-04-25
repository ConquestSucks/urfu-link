using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Presence.Application;
using Urfu.Link.Services.Presence.Domain;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;
using Urfu.Link.Services.Presence.Infrastructure.Persistence.Repositories;

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
            "presence.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        services.AddDbContextPool<PresenceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Primary")));

        services.AddScoped<ILastSeenRepository>(sp => new LastSeenRepository(
            sp.GetRequiredService<PresenceDbContext>(),
            sp.GetRequiredService<IOutboxWriter>(),
            ServiceName));

        return services;
    }
}


