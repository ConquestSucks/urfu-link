using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Media.Application;
using Urfu.Link.Services.Media.Domain;

namespace Urfu.Link.Services.Media.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServiceProfile(
            "media-service",
            "postgresql",
            KafkaTopicNames.MediaEvents,
            "media.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}


