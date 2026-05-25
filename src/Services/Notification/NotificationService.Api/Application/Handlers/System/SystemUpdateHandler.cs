using Urfu.Link.BuildingBlocks.Contracts.Integration.System;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.System;

public sealed class SystemUpdateHandler : INotificationHandler<SystemUpdateEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        SystemUpdateEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            integrationEvent.Title,
            integrationEvent.Body,
            imageUrl: null,
            deepLink: "urfulink://system/update");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["updateId"] = integrationEvent.UpdateId,
            ["version"] = integrationEvent.Version,
        });

        var groupKey = GroupKey.ForSystem($"update:{integrationEvent.UpdateId}");

        var drafts = new List<NotificationIntent>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            drafts.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: NotificationCategory.SystemUpdate,
                Severity: NotificationSeverity.Low,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(drafts);
    }
}
