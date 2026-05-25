using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;

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

            case "chat.thread.reply_posted.v1":
                {
                    var evt = payload.Deserialize<ChatThreadReplyPostedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatThreadReplyPostedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<ChatThreadReplyPostedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.reaction.added.v1":
                {
                    var evt = payload.Deserialize<ChatReactionAddedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatReactionAddedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<ChatReactionAddedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.reaction.removed.v1":
                {
                    var evt = payload.Deserialize<ChatReactionRemovedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatReactionRemovedEvent payload null");
                    await scope.GetRequiredService<NotificationLifecycleService>()
                        .ArchiveBySourceActionAsync(
                            NotificationSourceActions.ChatReaction(
                                evt.ConversationId,
                                evt.MessageId,
                                evt.UserId,
                                evt.Emoji),
                            evt.OccurredAtUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.message.pinned.v1":
                {
                    var evt = payload.Deserialize<ChatMessagePinnedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatMessagePinnedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<ChatMessagePinnedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.message.unpinned.v1":
                {
                    var evt = payload.Deserialize<ChatMessageUnpinnedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatMessageUnpinnedEvent payload null");
                    await scope.GetRequiredService<NotificationLifecycleService>()
                        .ArchiveBySourceActionAsync(
                            NotificationSourceActions.ChatPin(evt.ConversationId, evt.MessageId),
                            evt.OccurredAtUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.participant_role_changed.v1":
                {
                    var evt = payload.Deserialize<ChatParticipantRoleChangedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatParticipantRoleChangedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<ChatParticipantRoleChangedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.participant_left.v1":
                {
                    var evt = payload.Deserialize<ChatParticipantLeftEvent>(JsonOptions)
                        ?? throw new JsonException("ChatParticipantLeftEvent payload null");
                    await scope.GetRequiredService<NotificationLifecycleService>()
                        .ArchiveBySourceActionAsync(
                            NotificationSourceActions.ChatParticipant(evt.ConversationId, evt.UserId),
                            evt.OccurredAtUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "chat.message.deleted.v1":
                {
                    var evt = payload.Deserialize<ChatMessageDeletedEvent>(JsonOptions)
                        ?? throw new JsonException("ChatMessageDeletedEvent payload null");
                    var lifecycle = scope.GetRequiredService<NotificationLifecycleService>();
                    await lifecycle.ArchiveBySourceActionAsync(
                        NotificationSourceActions.ChatMessage(evt.ConversationId, evt.MessageId),
                        evt.OccurredAtUtc,
                        cancellationToken).ConfigureAwait(false);
                    await lifecycle.ArchiveBySourceActionAsync(
                        NotificationSourceActions.ChatThreadReply(evt.ConversationId, evt.MessageId),
                        evt.OccurredAtUtc,
                        cancellationToken).ConfigureAwait(false);
                    await lifecycle.ArchiveBySourceActionAsync(
                        NotificationSourceActions.ChatPin(evt.ConversationId, evt.MessageId),
                        evt.OccurredAtUtc,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "chat.message.edited.v1":
            case "chat.conversation_archived.v1":
            case "chat.thread.subscription_changed.v1":
                break;

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
