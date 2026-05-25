using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Builds <see cref="NotificationAggregate"/> instances from intents, providing the
/// timestamp from a single point so partition routing is deterministic across
/// pipeline stages.
/// </summary>
public sealed class NotificationFactory(TimeProvider timeProvider)
{
    public NotificationAggregate FromIntent(NotificationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var sourceActionId = string.IsNullOrWhiteSpace(intent.SourceActionId)
            ? NotificationSourceActions.Fallback(intent.RecipientUserId, intent.SourceEventId, intent.SourceEventType)
            : intent.SourceActionId;

        return NotificationAggregate.Create(
            recipientUserId: intent.RecipientUserId,
            category: intent.Category,
            severity: intent.Severity,
            content: intent.Content,
            data: intent.Data,
            groupKey: intent.GroupKey,
            sourceEventId: intent.SourceEventId,
            sourceEventType: intent.SourceEventType,
            createdAtUtc: timeProvider.GetUtcNow(),
            sourceActionId: sourceActionId,
            priority: intent.Priority,
            type: intent.Type,
            actor: intent.Actor,
            entity: intent.Entity,
            actions: intent.Actions);
    }
}
