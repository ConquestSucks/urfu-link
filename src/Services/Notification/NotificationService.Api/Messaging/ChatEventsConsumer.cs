using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Routing;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class ChatEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ChatEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.ChatEvents;

    protected override string GroupId => "notification-service-chat-v1";

    protected override string DedupKeyPrefix => "notif:chat-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "chat.message.sent.v1":
                {
                    var evt = payload.Deserialize<ChatMessageSentEvent>(JsonOptions)
                        ?? throw new JsonException("ChatMessageSentEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<ChatMessageSentHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.mention.created.v1":
                {
                    var evt = payload.Deserialize<ChatMentionCreatedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatMentionCreatedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<ChatMentionCreatedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.participant_joined.v1":
                {
                    var evt = payload.Deserialize<ChatParticipantJoinedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatParticipantJoinedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<AdminChatInviteHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.discipline_conversation_created.v1":
                {
                    var evt = payload.Deserialize<ChatDisciplineConversationCreatedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatDisciplineConversationCreatedEvent payload null");
                    var handler = scope.GetRequiredService<ChatDisciplineConversationCreatedHandler>();
                    await handler.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            default:
                break;
        }
    }
}
