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
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Application.Calls;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Messaging;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Endpoints.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Authorization;
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

        services.AddOptions<InternalGrpcAuthOptions>()
            .Bind(configuration.GetSection(InternalGrpcAuthOptions.SectionName));
        services.AddHttpClient(KeycloakClientCredentialsGrpcBearerTokenProvider.HttpClientName);
        services.AddSingleton<IGrpcBearerTokenProvider, KeycloakClientCredentialsGrpcBearerTokenProvider>();

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
        services.AddScoped<IThreadSubscriptionRepository, ThreadSubscriptionRepository>();
        services.AddHostedService<MongoIndexInitializer>();

        services.AddOptions<ChatOptions>()
            .Bind(configuration.GetSection(ChatOptions.SectionName));
        services.AddSingleton<IDisciplineRoleResolver, DefaultDisciplineRoleResolver>();

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
            // attachment validation/grant; after retries are exhausted SendMessage must still fail.
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

        services.AddOptions<DisciplineServiceClientOptions>()
            .Bind(configuration.GetSection(DisciplineServiceClientOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<DisciplineServiceClientOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.Address))
            {
                throw new InvalidOperationException(
                    "DisciplineService gRPC address is not configured (GrpcClients:DisciplineService:Address).");
            }

            // Built-in Grpc.Net retry on transient errors. ChatService falls back to its local
            // discipline-conversation projection if the bootstrap call fails, so this only smooths
            // out short blips, not full outages.
            var retryPolicy = new RetryPolicy
            {
                MaxAttempts = 3,
                InitialBackoff = TimeSpan.FromMilliseconds(100),
                MaxBackoff = TimeSpan.FromSeconds(2),
                BackoffMultiplier = 2,
                RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.DeadlineExceeded },
            };

            var channel = GrpcChannel.ForAddress(opts.Address, new GrpcChannelOptions
            {
                ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig { Names = { MethodName.Default }, RetryPolicy = retryPolicy },
                    },
                },
            });
            return new Urfu.Link.Services.Disciplines.Grpc.InternalApi.InternalApiClient(channel);
        });
        services.AddSingleton<IDisciplineServiceClient, DisciplineServiceClient>();

        services.AddOptions<UserServiceClientOptions>()
            .Bind(configuration.GetSection(UserServiceClientOptions.SectionName));
        var userServiceAddress = configuration[$"{UserServiceClientOptions.SectionName}:Address"];
        if (!string.IsNullOrWhiteSpace(userServiceAddress))
        {
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<UserServiceClientOptions>>().Value;
                var retryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    InitialBackoff = TimeSpan.FromMilliseconds(100),
                    MaxBackoff = TimeSpan.FromSeconds(2),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.DeadlineExceeded },
                };
                var channel = GrpcChannel.ForAddress(opts.Address, new GrpcChannelOptions
                {
                    ServiceConfig = new ServiceConfig
                    {
                        MethodConfigs =
                        {
                            new MethodConfig { Names = { MethodName.Default }, RetryPolicy = retryPolicy },
                        },
                    },
                });
                return new Urfu.Link.Services.User.Grpc.InternalApi.InternalApiClient(channel);
            });
            services.AddSingleton<Urfu.Link.Services.Chat.Application.Users.IUserServiceClient, UserServiceClient>();
        }
        else
        {
            // Stub для тестов и on-prem без UserService.
            services.AddSingleton<Urfu.Link.Services.Chat.Application.Users.IUserServiceClient, NoopUserServiceClient>();
        }

        services.AddOptions<PresenceServiceClientOptions>()
            .Bind(configuration.GetSection(PresenceServiceClientOptions.SectionName));
        var presenceAddress = configuration[$"{PresenceServiceClientOptions.SectionName}:Address"];
        if (!string.IsNullOrWhiteSpace(presenceAddress))
        {
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<PresenceServiceClientOptions>>().Value;
                var retryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    InitialBackoff = TimeSpan.FromMilliseconds(100),
                    MaxBackoff = TimeSpan.FromSeconds(2),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.DeadlineExceeded },
                };

                var channel = GrpcChannel.ForAddress(opts.Address, new GrpcChannelOptions
                {
                    ServiceConfig = new ServiceConfig
                    {
                        MethodConfigs =
                        {
                            new MethodConfig { Names = { MethodName.Default }, RetryPolicy = retryPolicy },
                        },
                    },
                });
                return new Urfu.Link.Services.Presence.Grpc.InternalApi.InternalApiClient(channel);
            });
            services.AddSingleton<Urfu.Link.Services.Chat.Application.Presence.IPresenceServiceClient,
                PresenceServiceClient>();
        }
        else
        {
            // Stub for tests and on-prem deployments without PresenceService.
            services.AddSingleton<Urfu.Link.Services.Chat.Application.Presence.IPresenceServiceClient,
                NoopPresenceServiceClient>();
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ChatEventDispatcher>();
        services.AddScoped<OpenDirectConversationService>();
        services.AddScoped<SendMessageService>();
        services.AddScoped<MarkDeliveredService>();
        services.AddScoped<MarkReadService>();
        services.AddScoped<EditMessageService>();
        services.AddScoped<DeleteMessageService>();
        services.AddScoped<ForwardMessagesService>();
        services.AddScoped<AddReactionService>();
        services.AddScoped<RemoveReactionService>();
        services.AddScoped<PinMessageService>();
        services.AddScoped<UnpinMessageService>();
        services.AddScoped<GetUserConversationsQuery>();
        services.AddScoped<GetConversationQuery>();
        services.AddScoped<GetPinnedMessagesQuery>();
        services.AddScoped<GetConversationParticipantsQuery>();
        services.AddScoped<GetConversationMessagesQuery>();
        services.AddScoped<GetReadReceiptsQuery>();
        services.AddScoped<ReplyInThreadService>();
        services.AddScoped<JoinThreadService>();
        services.AddScoped<LeaveThreadService>();
        services.AddScoped<GetThreadMessagesQuery>();
        services.AddScoped<GetUserActiveThreadsQuery>();
        services.AddScoped<SearchMessagesQuery>();
        services.AddScoped<CallSystemMessageService>();
        services.AddScoped<DisciplineConversationService>();
        services.AddScoped<Urfu.Link.Services.Chat.Application.Mentions.MentionResolver>();
        services.AddHostedService<DisciplineEventConsumer>();
        services.AddHostedService<CallEventConsumer>();

        // Per-user fixed window for /chat/search (issue #213 — 30 req/min per user).
        // ChatSearchRateLimitFilter resolves this limiter via [FromKeyedServices(name)].
        services.AddRedisRateLimiter(
            ChatSearchRateLimiterPolicy.Name,
            window: TimeSpan.FromMinutes(1),
            maxRequests: 30);

        services.AddSingleton<IChatBroadcaster, ChatBroadcaster>();

        return services;
    }
}

