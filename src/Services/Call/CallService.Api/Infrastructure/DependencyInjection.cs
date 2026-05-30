using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Call.Application;
using Urfu.Link.Services.Call.Application.Calls;
using Urfu.Link.Services.Call.Application.Chat;
using Urfu.Link.Services.Call.Domain;
using Urfu.Link.Services.Call.Infrastructure.Grpc;
using Urfu.Link.Services.Call.Infrastructure.Redis;
using Urfu.Link.Services.Call.Realtime;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Grpc.Core;

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
        services.AddOptions<CallOptions>()
            .Bind(configuration.GetSection(CallOptions.SectionName));
        services.AddOptions<LiveKitOptions>()
            .Bind(configuration.GetSection(LiveKitOptions.SectionName));
        services.AddOptions<ChatConversationClientOptions>()
            .Bind(configuration.GetSection(ChatConversationClientOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatConversationClientOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.Address))
            {
                throw new InvalidOperationException(
                    "ChatService gRPC address is not configured (GrpcClients:ChatService:Address).");
            }

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
        services.AddSingleton(sp => new Urfu.Link.Services.Chat.Grpc.InternalApi.InternalApiClient(
            sp.GetRequiredService<GrpcChannel>()));
        services.AddSingleton<IChatConversationClient, ChatConversationClient>();
        services.AddSingleton<ICallSessionStore, RedisCallSessionStore>();
        services.AddSingleton<ICallBroadcaster, CallBroadcaster>();
        services.AddSingleton<LiveKitTokenProvider>();
        services.AddScoped<CallEventDispatcher>();
        services.AddScoped<CallSessionService>();
        services.AddScoped<SampleEventDispatcher>();

        return services;
    }
}

