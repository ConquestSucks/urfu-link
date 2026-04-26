using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace Urfu.Link.Services.Notification.Application.Handlers.Admin;

/// <summary>
/// Reacts to <see cref="UserDeletedEvent"/> by deactivating every push device of the
/// user and removing their unread notifications. Read history is preserved for audit.
/// </summary>
public sealed class UserDeletedHandler(
    NotificationDbContext db,
    IPushDeviceRepository pushDevices,
    TimeProvider timeProvider,
    ILogger<UserDeletedHandler> logger)
{
    public async Task HandleAsync(UserDeletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var userId = integrationEvent.UserId;
        var now = timeProvider.GetUtcNow();

        var devices = await pushDevices.ListActiveByUserForUpdateAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (var device in devices)
        {
            device.Deactivate(now, "user_deleted");
        }

        await pushDevices.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var deletedNotifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId && n.ReadAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "User {UserId} deleted: deactivated {DeviceCount} push devices, dropped {NotificationCount} unread notifications",
                userId,
                devices.Count,
                deletedNotifications);
        }
    }
}
