using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

public sealed class ChatReactionAddedHandler : INotificationHandler<ChatReactionAddedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatReactionAddedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        if (integrationEvent.MessageAuthorId is null ||
            integrationEvent.MessageAuthorId == Guid.Empty ||
            integrationEvent.MessageAuthorId == integrationEvent.UserId)
        {
            return Task.FromResult<IReadOnlyList<NotificationIntent>>([]);
        }

        if (!Guid.TryParse(integrationEvent.ConversationId, out var conversationGuid))
        {
            conversationGuid = StableGuids.From(integrationEvent.ConversationId);
        }

        var content = NotificationContent.Create(
            "Новая реакция",
            $"На ваше сообщение поставили {integrationEvent.Emoji}",
            imageUrl: null,
            deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}/msg/{integrationEvent.MessageId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["conversationId"] = integrationEvent.ConversationId,
            ["messageId"] = integrationEvent.MessageId.ToString("N", CultureInfo.InvariantCulture),
            ["reactorId"] = integrationEvent.UserId.ToString("N", CultureInfo.InvariantCulture),
            ["emoji"] = integrationEvent.Emoji,
        });

        var intent = new NotificationIntent(
            RecipientUserId: integrationEvent.MessageAuthorId.Value,
            Category: NotificationCategory.ChatReaction,
            Severity: NotificationSeverity.Low,
            Content: content,
            Data: data,
            GroupKey: GroupKey.ForDirectChat(conversationGuid),
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType,
            SourceActionId: NotificationSourceActions.ChatReaction(
                integrationEvent.ConversationId,
                integrationEvent.MessageId,
                integrationEvent.UserId,
                integrationEvent.Emoji),
            Priority: NotificationPriority.Reaction,
            Actor: new NotificationActor(integrationEvent.UserId, null, null),
            SuppressWhenViewingContextKey: NotificationViewingContexts.ChatConversation(integrationEvent.ConversationId));

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([intent]);
    }
}
