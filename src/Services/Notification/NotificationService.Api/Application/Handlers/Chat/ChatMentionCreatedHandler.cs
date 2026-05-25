using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

/// <summary>
/// Handles standalone mention events (e.g. late-edit mentions). Each mentioned user
/// gets a high-severity ChatMessageMention intent.
/// </summary>
public sealed class ChatMentionCreatedHandler : INotificationHandler<ChatMentionCreatedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatMentionCreatedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var drafts = new List<NotificationIntent>(integrationEvent.MentionedUserIds.Count);
        if (!Guid.TryParse(integrationEvent.ConversationId, out var conversationGuid))
        {
            conversationGuid = StableGuids.From(integrationEvent.ConversationId);
        }

        var content = NotificationContent.Create(
            "Вас упомянули",
            "Вас упомянули в чате",
            imageUrl: null,
            deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}/msg/{integrationEvent.MessageId:N}");

        foreach (var userId in integrationEvent.MentionedUserIds)
        {
            if (userId == integrationEvent.SenderId)
            {
                continue;
            }

            var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["conversationId"] = integrationEvent.ConversationId,
                ["messageId"] = integrationEvent.MessageId.ToString("N", CultureInfo.InvariantCulture),
                ["senderId"] = integrationEvent.SenderId.ToString("N", CultureInfo.InvariantCulture),
            });

            drafts.Add(new NotificationIntent(
                RecipientUserId: userId,
                Category: NotificationCategory.ChatMessageMention,
                Severity: NotificationSeverity.High,
                Content: content,
                Data: data,
                GroupKey: GroupKey.ForChatMention(conversationGuid),
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.ChatMessage(integrationEvent.ConversationId, integrationEvent.MessageId),
                Priority: NotificationPriority.Mention,
                Actor: new NotificationActor(integrationEvent.SenderId, null, null),
                SuppressWhenViewingContextKey: NotificationViewingContexts.ChatConversation(integrationEvent.ConversationId)));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(drafts);
    }
}
