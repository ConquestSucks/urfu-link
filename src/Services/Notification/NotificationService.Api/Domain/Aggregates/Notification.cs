using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Events;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class Notification
{
    public const int SourceEventTypeMaxLength = 128;

    private readonly List<IIntegrationEvent> _domainEvents = [];
    private readonly List<Delivery> _deliveries = [];

    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public NotificationCategory Category { get; private set; }

    public NotificationSeverity Severity { get; private set; }

    public NotificationContent Content { get; private set; } = null!;

    public NotificationData Data { get; private set; } = NotificationData.Empty;

    public GroupKey? GroupKey { get; private set; }

    public Guid SourceEventId { get; private set; }

    public string SourceEventType { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public IReadOnlyList<Delivery> Deliveries => _deliveries;

    public IReadOnlyList<IIntegrationEvent> DomainEvents => _domainEvents;

    private Notification()
    {
    }

    public static Notification Create(
        Guid recipientUserId,
        NotificationCategory category,
        NotificationSeverity severity,
        NotificationContent content,
        NotificationData data,
        GroupKey? groupKey,
        Guid sourceEventId,
        string sourceEventType,
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

        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventType);

        var trimmedType = sourceEventType.Trim();
        if (trimmedType.Length > SourceEventTypeMaxLength)
        {
            throw new ArgumentException($"Source event type exceeds {SourceEventTypeMaxLength} characters.", nameof(sourceEventType));
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Category = category,
            Severity = severity,
            Content = content,
            Data = data,
            GroupKey = groupKey,
            SourceEventId = sourceEventId,
            SourceEventType = trimmedType,
            CreatedAtUtc = createdAtUtc,
        };

        notification._domainEvents.Add(new NotificationCreatedEvent(
            notification.Id,
            recipientUserId,
            category,
            severity,
            groupKey?.Value,
            sourceEventId,
            trimmedType));

        return notification;
    }

    public bool MarkRead(DateTimeOffset readAtUtc)
    {
        if (ReadAtUtc.HasValue)
        {
            return false;
        }

        ReadAtUtc = readAtUtc;
        _domainEvents.Add(new NotificationReadEvent(Id, RecipientUserId, readAtUtc));
        return true;
    }

    public void AddDelivery(Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (delivery.NotificationId != Id)
        {
            throw new ArgumentException(
                "Delivery does not belong to this notification.",
                nameof(delivery));
        }

        _deliveries.Add(delivery);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
