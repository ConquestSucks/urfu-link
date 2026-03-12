using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.User.Application;
using Urfu.Link.Services.User.Domain;

namespace Urfu.Link.Services.User.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServiceProfile(
            "user-service",
            "postgresql",
            KafkaTopicNames.UserEvents,
            "user.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}


