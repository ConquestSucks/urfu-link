using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Discipline;

public sealed class DisciplineDeadlineHandler : INotificationHandler<DisciplineDeadlineApproachingEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        DisciplineDeadlineApproachingEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var dueLocal = integrationEvent.DueAtUtc.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
        var content = NotificationContent.Create(
            "Скоро дедлайн",
            $"{integrationEvent.AssignmentTitle} — до {dueLocal}",
            imageUrl: null,
            deepLink: $"urfulink://discipline/{integrationEvent.DisciplineId:N}/assignment/{integrationEvent.AssignmentId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disciplineId"] = integrationEvent.DisciplineId.ToString("N", CultureInfo.InvariantCulture),
            ["assignmentId"] = integrationEvent.AssignmentId.ToString("N", CultureInfo.InvariantCulture),
            ["dueAtUtc"] = integrationEvent.DueAtUtc.ToString("o", CultureInfo.InvariantCulture),
        });

        var groupKey = GroupKey.ForDisciplineDeadline(integrationEvent.DisciplineId, integrationEvent.AssignmentId);

        var drafts = new List<NotificationDraft>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            drafts.Add(new NotificationDraft(
                RecipientUserId: recipientId,
                Category: NotificationCategory.DisciplineDeadline,
                Severity: NotificationSeverity.High,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType));
        }

        return Task.FromResult<IReadOnlyList<NotificationDraft>>(drafts);
    }
}
