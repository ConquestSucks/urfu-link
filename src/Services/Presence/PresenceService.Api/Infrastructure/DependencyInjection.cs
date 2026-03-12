using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Presence.Application;
using Urfu.Link.Services.Presence.Domain;

namespace Urfu.Link.Services.Presence.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddPresenceModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServiceProfile(
            "presence-service",
            "redis",
            KafkaTopicNames.PresenceEvents,
            "presence.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}


