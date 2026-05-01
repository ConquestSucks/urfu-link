using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Interfaces;

public interface IBadgeStore
{
    Task<int> IncrementAsync(Guid userId, NotificationCategory category, CancellationToken cancellationToken);

    Task<int> DecrementAsync(Guid userId, NotificationCategory category, CancellationToken cancellationToken);

    Task ResetAsync(Guid userId, CancellationToken cancellationToken);

    Task<BadgeSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> SetSnapshotAsync(Guid userId, BadgeSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed record BadgeSnapshot(int Total, IReadOnlyDictionary<NotificationCategory, int> PerCategory)
{
    public static BadgeSnapshot Empty { get; } = new(
        0,
        new Dictionary<NotificationCategory, int>());
}
