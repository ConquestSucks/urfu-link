using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Discipline;

public sealed class UserEnrolledHandler : INotificationHandler<UserEnrolledEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        UserEnrolledEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Зачисление в дисциплину",
            "Вы зачислены в дисциплину",
            imageUrl: null,
            deepLink: $"urfulink://discipline/{integrationEvent.DisciplineId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disciplineId"] = integrationEvent.DisciplineId.ToString("N", CultureInfo.InvariantCulture),
            ["role"] = integrationEvent.Role.ToString(),
        });

        var draft = new NotificationDraft(
            RecipientUserId: integrationEvent.UserId,
            Category: NotificationCategory.DisciplineAnnouncement,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: GroupKey.ForDisciplineAnnouncement(integrationEvent.DisciplineId),
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType);

        return Task.FromResult<IReadOnlyList<NotificationDraft>>([draft]);
    }
}
