using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Discipline;

public sealed class EnrollmentRoleChangedHandler : INotificationHandler<EnrollmentRoleChangedEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        EnrollmentRoleChangedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Изменена роль",
            $"Ваша роль изменена на {integrationEvent.NewRole}",
            imageUrl: null,
            deepLink: $"urfulink://discipline/{integrationEvent.DisciplineId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disciplineId"] = integrationEvent.DisciplineId.ToString("N", CultureInfo.InvariantCulture),
            ["oldRole"] = integrationEvent.OldRole.ToString(),
            ["newRole"] = integrationEvent.NewRole.ToString(),
        });

        var draft = new NotificationDraft(
            RecipientUserId: integrationEvent.UserId,
            Category: NotificationCategory.AdminRoleChanged,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType);

        return Task.FromResult<IReadOnlyList<NotificationDraft>>([draft]);
    }
}
