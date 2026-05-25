namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class NotificationDedupKey
{
    public Guid RecipientUserId { get; private set; }

    public Guid SourceEventId { get; private set; }

    public string NotificationType { get; private set; } = null!;

    public Guid NotificationId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private NotificationDedupKey()
    {
    }

    public static NotificationDedupKey Create(
        Guid recipientUserId,
        Guid sourceEventId,
        string notificationType,
        Guid notificationId,
        DateTimeOffset createdAtUtc)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient user id is required.", nameof(recipientUserId));
        }

        if (sourceEventId == Guid.Empty)
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(notificationType);

        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification id is required.", nameof(notificationId));
        }

        return new NotificationDedupKey
        {
            RecipientUserId = recipientUserId,
            SourceEventId = sourceEventId,
            NotificationType = notificationType.Trim(),
            NotificationId = notificationId,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
