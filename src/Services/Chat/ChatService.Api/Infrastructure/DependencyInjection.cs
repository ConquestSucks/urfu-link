using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Domain;

namespace Urfu.Link.Services.Chat.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddChatModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServiceProfile(
            "chat-service",
            "mongodb",
            KafkaTopicNames.ChatEvents,
            "chat.sample.v1"));
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}


