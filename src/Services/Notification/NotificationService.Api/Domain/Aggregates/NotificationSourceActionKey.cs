using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class NotificationSourceActionKey
{
    public Guid RecipientUserId { get; private set; }

    public string SourceActionId { get; private set; } = null!;

    public Guid NotificationId { get; private set; }

    public DateTimeOffset NotificationCreatedAtUtc { get; private set; }

    public NotificationPriority Priority { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private NotificationSourceActionKey()
    {
    }

    public static NotificationSourceActionKey Create(
        Guid recipientUserId,
        string sourceActionId,
        Guid notificationId,
        DateTimeOffset notificationCreatedAtUtc,
        NotificationPriority priority,
        DateTimeOffset createdAtUtc)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient user id is required.", nameof(recipientUserId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceActionId);

        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification id is required.", nameof(notificationId));
        }

        return new NotificationSourceActionKey
        {
            RecipientUserId = recipientUserId,
            SourceActionId = sourceActionId.Trim(),
            NotificationId = notificationId,
            NotificationCreatedAtUtc = notificationCreatedAtUtc,
            Priority = priority,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void PointTo(Guid notificationId, DateTimeOffset notificationCreatedAtUtc, NotificationPriority priority)
    {
        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification id is required.", nameof(notificationId));
        }

        NotificationId = notificationId;
        NotificationCreatedAtUtc = notificationCreatedAtUtc;
        Priority = priority;
    }
}
