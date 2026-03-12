using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Call.Application;
using Urfu.Link.Services.Call.Domain;

namespace Urfu.Link.Services.Call.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddCallModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServiceProfile(
            "call-service",
            "signaling",
            KafkaTopicNames.CallEvents,
            "call.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}


