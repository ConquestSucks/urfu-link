using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Admin;

public sealed class AdminRoleChangedHandler : INotificationHandler<UserRoleChangedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        UserRoleChangedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Изменена роль",
            $"Ваша роль в системе изменена на {integrationEvent.NewRole}",
            imageUrl: null,
            deepLink: "urfulink://account/role");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oldRole"] = integrationEvent.OldRole,
            ["newRole"] = integrationEvent.NewRole,
        });

        var draft = new NotificationIntent(
            RecipientUserId: integrationEvent.UserId,
            Category: NotificationCategory.AdminRoleChanged,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType);

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([draft]);
    }
}
