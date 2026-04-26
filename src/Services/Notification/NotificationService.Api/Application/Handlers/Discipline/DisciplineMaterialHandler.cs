using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Discipline;

public sealed class DisciplineMaterialHandler : INotificationHandler<DisciplineMaterialPublishedEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        DisciplineMaterialPublishedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Новый материал в дисциплине",
            string.IsNullOrWhiteSpace(integrationEvent.Description) ? integrationEvent.Title : integrationEvent.Description,
            imageUrl: null,
            deepLink: $"urfulink://discipline/{integrationEvent.DisciplineId:N}/material/{integrationEvent.MaterialId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disciplineId"] = integrationEvent.DisciplineId.ToString("N", CultureInfo.InvariantCulture),
            ["materialId"] = integrationEvent.MaterialId.ToString("N", CultureInfo.InvariantCulture),
            ["title"] = integrationEvent.Title,
        });

        var groupKey = GroupKey.ForDisciplineMaterial(integrationEvent.DisciplineId);

        var drafts = new List<NotificationDraft>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.AuthorTeacherId)
            {
                continue;
            }

            drafts.Add(new NotificationDraft(
                RecipientUserId: recipientId,
                Category: NotificationCategory.DisciplineMaterial,
                Severity: NotificationSeverity.Normal,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType));
        }

        return Task.FromResult<IReadOnlyList<NotificationDraft>>(drafts);
    }
}
