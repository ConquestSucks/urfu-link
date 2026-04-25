using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using MediaService.Api.Grpc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddChatModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new ServiceProfile(
            "chat-service",
            "mongodb",
            KafkaTopicNames.ChatEvents,
            "chat.message.sent.v1"));

        services.AddOptions<ChatMongoOptions>()
            .Bind(configuration.GetSection(ChatMongoOptions.SectionName))
            .PostConfigure(opts =>
            {
                if (string.IsNullOrWhiteSpace(opts.ConnectionString))
                {
                    opts.ConnectionString = configuration.GetConnectionString("Primary") ?? string.Empty;
                }
            });
        services.AddSingleton<ChatMongoContext>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddHostedService<MongoIndexInitializer>();

        services.AddOptions<MediaServiceClientOptions>()
            .Bind(configuration.GetSection(MediaServiceClientOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MediaServiceClientOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.Address))
            {
                throw new InvalidOperationException(
                    "MediaService gRPC address is not configured (GrpcClients:MediaService:Address).");
            }

            // Built-in Grpc.Net retry on transient errors. ChatService relies on MediaService for
            // attachment validation/grant; a single brief outage shouldn't fail SendMessage.
            var retryPolicy = new RetryPolicy
            {
                MaxAttempts = 3,
                InitialBackoff = TimeSpan.FromMilliseconds(100),
                MaxBackoff = TimeSpan.FromSeconds(2),
                BackoffMultiplier = 2,
                RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.DeadlineExceeded },
            };
            return GrpcChannel.ForAddress(opts.Address, new GrpcChannelOptions
            {
                ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig { Names = { MethodName.Default }, RetryPolicy = retryPolicy },
                    },
                },
            });
        });
        services.AddSingleton(sp => new InternalApi.InternalApiClient(sp.GetRequiredService<GrpcChannel>()));
        services.AddSingleton<IMediaServiceClient, MediaServiceClient>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ChatEventDispatcher>();
        services.AddScoped<OpenDirectConversationService>();
        services.AddScoped<SendMessageService>();
        services.AddScoped<MarkDeliveredService>();
        services.AddScoped<MarkReadService>();
        services.AddScoped<GetUserConversationsQuery>();
        services.AddScoped<GetConversationQuery>();
        services.AddScoped<GetConversationMessagesQuery>();

        services.AddSingleton<IChatBroadcaster, ChatBroadcaster>();

        return services;
    }
}


