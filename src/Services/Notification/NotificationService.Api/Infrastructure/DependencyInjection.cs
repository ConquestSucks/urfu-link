using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Application;
using Urfu.Link.Services.Notification.Domain;

namespace Urfu.Link.Services.Notification.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServiceProfile(
            "notification-service",
            "stateless",
            KafkaTopicNames.NotificationEvents,
            "notification.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}


