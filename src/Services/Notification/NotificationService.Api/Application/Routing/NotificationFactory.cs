using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Builds <see cref="NotificationAggregate"/> instances from drafts, providing the
/// timestamp from a single point so partition routing is deterministic across
/// pipeline stages.
/// </summary>
public sealed class NotificationFactory(TimeProvider timeProvider)
{
    public NotificationAggregate FromDraft(NotificationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return NotificationAggregate.Create(
            recipientUserId: draft.RecipientUserId,
            category: draft.Category,
            severity: draft.Severity,
            content: draft.Content,
            data: draft.Data,
            groupKey: draft.GroupKey,
            sourceEventId: draft.SourceEventId,
            sourceEventType: draft.SourceEventType,
            createdAtUtc: timeProvider.GetUtcNow());
    }
}
