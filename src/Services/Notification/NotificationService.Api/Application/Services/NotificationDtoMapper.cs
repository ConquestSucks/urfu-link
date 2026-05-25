using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Application.Services;

internal static class NotificationDtoMapper
{
    public static NotificationDto Map(NotificationAggregate notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationDto(
            Id: notification.Id,
            RecipientUserId: notification.RecipientUserId,
            Type: notification.Type,
            Category: notification.Category,
            Severity: notification.Severity,
            Title: notification.Content.Title,
            Body: notification.Content.Body,
            ImageUrl: notification.Content.ImageUrl,
            DeepLink: notification.Content.DeepLink,
            Data: notification.Data.Values,
            Actor: notification.Actor is null
                ? null
                : new NotificationActorDto(notification.Actor.Id, notification.Actor.DisplayName, notification.Actor.AvatarUrl),
            Entity: notification.Entity is null
                ? null
                : new NotificationEntityDto(notification.Entity.Kind, notification.Entity.Id, notification.Entity.DisplayName),
            Actions: notification.Actions
                .Select(a => new NotificationActionDto(a.Id, a.Label, a.Kind, a.DeepLink))
                .ToArray(),
            GroupKey: notification.GroupKey?.Value,
            OccurrenceCount: notification.OccurrenceCount,
            CreatedAtUtc: notification.CreatedAtUtc,
            LastOccurrenceAtUtc: notification.LastOccurrenceAtUtc,
            ReadAtUtc: notification.ReadAtUtc,
            SeenAtUtc: notification.SeenAtUtc,
            SavedAtUtc: notification.SavedAtUtc,
            DoneAtUtc: notification.DoneAtUtc,
            ArchivedAtUtc: notification.ArchivedAtUtc,
            SnoozedUntilUtc: notification.SnoozedUntilUtc,
            ExpiresAtUtc: notification.ExpiresAtUtc,
            SourceActionId: notification.SourceActionId,
            Priority: notification.Priority,
            SupersededByNotificationId: notification.SupersededByNotificationId);
    }
}
