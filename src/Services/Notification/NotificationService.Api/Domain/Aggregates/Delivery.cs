using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class Delivery
{
    public const int AddressMaxLength = 1000;
    public const int ErrorMaxLength = 1024;
    public const int ProviderMessageIdMaxLength = 200;

    public Guid Id { get; private set; }

    public Guid NotificationId { get; private set; }

    public DeliveryChannel Channel { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public string Address { get; private set; } = null!;

    public PushProvider? Provider { get; private set; }

    public Guid? PushDeviceId { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? SkipReason { get; private set; }

    private Delivery()
    {
    }

    public static Delivery PendingPush(Guid notificationId, PushProvider provider, Guid pushDeviceId, string token)
    {
        ValidateNotificationId(notificationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (pushDeviceId == Guid.Empty)
        {
            throw new ArgumentException("Push device id is required.", nameof(pushDeviceId));
        }

        return new Delivery
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            Channel = DeliveryChannel.Push,
            Status = DeliveryStatus.Pending,
            Address = TrimAddress(token),
            Provider = provider,
            PushDeviceId = pushDeviceId,
        };
    }

    public static Delivery PendingEmail(Guid notificationId, string emailAddress)
    {
        ValidateNotificationId(notificationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        return new Delivery
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            Channel = DeliveryChannel.Email,
            Status = DeliveryStatus.Pending,
            Address = TrimAddress(emailAddress),
        };
    }

    public static Delivery PendingInApp(Guid notificationId, string userScopedAddress)
    {
        ValidateNotificationId(notificationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userScopedAddress);

        return new Delivery
        {
            Id = Guid.NewGuid(),
            NotificationId = notificationId,
            Channel = DeliveryChannel.InApp,
            Status = DeliveryStatus.Pending,
            Address = TrimAddress(userScopedAddress),
        };
    }

    public void MarkSent(DateTimeOffset attemptAtUtc, string? providerMessageId = null)
    {
        EnsureNotCompleted();
        Attempts++;
        Status = DeliveryStatus.Sent;
        LastAttemptAtUtc = attemptAtUtc;
        CompletedAtUtc = attemptAtUtc;
        NextAttemptAtUtc = null;
        LastError = null;
        if (providerMessageId is not null)
        {
            if (providerMessageId.Length > ProviderMessageIdMaxLength)
            {
                throw new ArgumentException($"Provider message id exceeds {ProviderMessageIdMaxLength} characters.", nameof(providerMessageId));
            }

            ProviderMessageId = providerMessageId;
        }
    }

    public void MarkDelivered(DateTimeOffset deliveredAtUtc)
    {
        if (Status is not (DeliveryStatus.Sent or DeliveryStatus.Delivered))
        {
            throw new InvalidOperationException($"Cannot mark {Status} delivery as delivered.");
        }

        Status = DeliveryStatus.Delivered;
        CompletedAtUtc = deliveredAtUtc;
    }

    public void RecordFailure(DateTimeOffset attemptAtUtc, string error, TimeSpan? backoff)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        EnsureNotCompleted();

        Attempts++;
        Status = DeliveryStatus.Pending;
        LastAttemptAtUtc = attemptAtUtc;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
        NextAttemptAtUtc = backoff.HasValue ? attemptAtUtc + backoff.Value : null;
    }

    public void MarkFinalFailed(DateTimeOffset attemptAtUtc, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        EnsureNotCompleted();

        Status = DeliveryStatus.Failed;
        LastAttemptAtUtc = attemptAtUtc;
        CompletedAtUtc = attemptAtUtc;
        NextAttemptAtUtc = null;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
    }

    public void MarkSkipped(DateTimeOffset atUtc, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureNotCompleted();

        Status = DeliveryStatus.Skipped;
        CompletedAtUtc = atUtc;
        NextAttemptAtUtc = null;
        SkipReason = reason.Length > ErrorMaxLength ? reason[..ErrorMaxLength] : reason;
    }

    private static string TrimAddress(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length > AddressMaxLength)
        {
            throw new ArgumentException($"Delivery address exceeds {AddressMaxLength} characters.", nameof(raw));
        }

        return trimmed;
    }

    private static void ValidateNotificationId(Guid notificationId)
    {
        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification id is required.", nameof(notificationId));
        }
    }

    private void EnsureNotCompleted()
    {
        if (Status is DeliveryStatus.Delivered or DeliveryStatus.Failed or DeliveryStatus.Skipped)
        {
            throw new InvalidOperationException($"Delivery already completed with status {Status}.");
        }
    }
}
