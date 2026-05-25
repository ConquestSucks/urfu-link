using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Discipline;

public sealed class DisciplineAnnouncementHandler : INotificationHandler<DisciplineAnnouncementEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        DisciplineAnnouncementEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            integrationEvent.Title,
            integrationEvent.Body,
            imageUrl: null,
            deepLink: $"urfulink://discipline/{integrationEvent.DisciplineId:N}/announcement/{integrationEvent.AnnouncementId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disciplineId"] = integrationEvent.DisciplineId.ToString("N", CultureInfo.InvariantCulture),
            ["announcementId"] = integrationEvent.AnnouncementId.ToString("N", CultureInfo.InvariantCulture),
            ["authorId"] = integrationEvent.AuthorTeacherId.ToString("N", CultureInfo.InvariantCulture),
        });

        var groupKey = GroupKey.ForDisciplineAnnouncement(integrationEvent.DisciplineId);

        var drafts = new List<NotificationIntent>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.AuthorTeacherId)
            {
                continue;
            }

            drafts.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: NotificationCategory.DisciplineAnnouncement,
                Severity: NotificationSeverity.High,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.DisciplineItem(
                    integrationEvent.DisciplineId,
                    integrationEvent.AnnouncementId,
                    "announcement"),
                Priority: NotificationPriority.PinSystemAdmin,
                Actor: new NotificationActor(integrationEvent.AuthorTeacherId, null, null)));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(drafts);
    }
}
