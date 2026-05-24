using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.System;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.System;

public sealed class SystemMaintenanceHandler : INotificationHandler<SystemMaintenanceEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        SystemMaintenanceEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var severity = integrationEvent.AffectsCriticalPath
            ? NotificationSeverity.Urgent
            : NotificationSeverity.High;

        var content = NotificationContent.Create(
            integrationEvent.Title,
            integrationEvent.Body,
            imageUrl: null,
            deepLink: $"urfulink://system/maintenance/{integrationEvent.MaintenanceId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["maintenanceId"] = integrationEvent.MaintenanceId.ToString("N", CultureInfo.InvariantCulture),
            ["startsAtUtc"] = integrationEvent.StartsAtUtc.ToString("o", CultureInfo.InvariantCulture),
            ["endsAtUtc"] = integrationEvent.EndsAtUtc.ToString("o", CultureInfo.InvariantCulture),
        });

        var groupKey = GroupKey.ForSystem($"maintenance:{integrationEvent.MaintenanceId:N}");

        var drafts = new List<NotificationDraft>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            drafts.Add(new NotificationDraft(
                RecipientUserId: recipientId,
                Category: NotificationCategory.SystemMaintenance,
                Severity: severity,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType));
        }

        return Task.FromResult<IReadOnlyList<NotificationDraft>>(drafts);
    }
}
