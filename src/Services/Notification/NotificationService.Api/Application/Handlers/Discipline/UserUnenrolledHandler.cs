using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Discipline;

public sealed class UserUnenrolledHandler : INotificationHandler<UserUnenrolledEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        UserUnenrolledEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Отчисление из дисциплины",
            "Вы больше не состоите в дисциплине",
            imageUrl: null,
            deepLink: $"urfulink://discipline/{integrationEvent.DisciplineId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disciplineId"] = integrationEvent.DisciplineId.ToString("N", CultureInfo.InvariantCulture),
        });

        var intent = new NotificationIntent(
            RecipientUserId: integrationEvent.UserId,
            Category: NotificationCategory.DisciplineUnenrollment,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: GroupKey.ForDisciplineAnnouncement(integrationEvent.DisciplineId),
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType,
            SourceActionId: NotificationSourceActions.DisciplineUser(
                integrationEvent.DisciplineId,
                integrationEvent.UserId,
                "unenrollment"),
            Priority: NotificationPriority.PinSystemAdmin);

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([intent]);
    }
}
