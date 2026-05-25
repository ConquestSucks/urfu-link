using Urfu.Link.Services.Notification.Domain.Enums;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Domain.Interfaces;

public interface INotificationRepository
{
    Task<bool> TryInsertAsync(NotificationAggregate notification, CancellationToken cancellationToken);

    Task<NotificationUpsertResult> UpsertAsync(NotificationAggregate notification, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationAggregate>> ArchiveBySourceActionAsync(
        string sourceActionId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken);

    Task<NotificationAggregate?> GetByIdAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationAggregate>> ListAsync(
        Guid recipientUserId,
        NotificationListFilter filter,
        DateTimeOffset? cursorCreatedAtUtc,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationAggregate>> ListForBulkAsync(
        Guid recipientUserId,
        NotificationListFilter filter,
        IReadOnlyList<Guid>? ids,
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

    Task<NotificationBadgeCounts> CountBadgeAsync(Guid recipientUserId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record NotificationListFilter(
    NotificationCategory? Category = null,
    string? Type = null,
    NotificationSeverity? Severity = null,
    string? Status = null,
    string? Query = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null)
{
    public bool IsUnread => string.Equals(Status, "unread", StringComparison.OrdinalIgnoreCase);
}

public sealed record NotificationBadgeCounts(
    int TotalUnread,
    int TotalUnseen,
    int UrgentUnread,
    IReadOnlyDictionary<NotificationCategory, int> PerCategory,
    IReadOnlyDictionary<string, int> PerType);

public enum NotificationUpsertStatus
{
    Created,
    Updated,
    Skipped,
}

public sealed record NotificationUpsertResult(NotificationUpsertStatus Status, NotificationAggregate? Notification)
{
    public static NotificationUpsertResult Created(NotificationAggregate notification) =>
        new(NotificationUpsertStatus.Created, notification);

    public static NotificationUpsertResult Updated(NotificationAggregate notification) =>
        new(NotificationUpsertStatus.Updated, notification);

    public static NotificationUpsertResult Skipped(NotificationAggregate? notification = null) =>
        new(NotificationUpsertStatus.Skipped, notification);
}
