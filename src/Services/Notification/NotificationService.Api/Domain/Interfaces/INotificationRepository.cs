using Urfu.Link.Services.Notification.Domain.Enums;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Domain.Interfaces;

public interface INotificationRepository
{
    Task<bool> TryInsertAsync(NotificationAggregate notification, CancellationToken cancellationToken);

    Task<NotificationAggregate?> GetByIdAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationAggregate>> ListAsync(
        Guid recipientUserId,
        NotificationCategory? category,
        bool unreadOnly,
        DateTimeOffset? cursorCreatedAtUtc,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken);

    Task<int> MarkAllAsReadAsync(
        Guid recipientUserId,
        NotificationCategory? category,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<NotificationCategory, int>> CountUnreadPerCategoryAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
