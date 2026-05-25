using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Events;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class Notification
{
    public const int SourceEventTypeMaxLength = 128;
    public const int SourceActionIdMaxLength = 256;

    private readonly List<IIntegrationEvent> _domainEvents = [];
    private readonly List<Delivery> _deliveries = [];

    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public NotificationCategory Category { get; private set; }

    public NotificationSeverity Severity { get; private set; }

    public string Type { get; private set; } = null!;

    public NotificationContent Content { get; private set; } = null!;

    public NotificationData Data { get; private set; } = NotificationData.Empty;

    public NotificationActor? Actor { get; private set; }

    public NotificationEntity? Entity { get; private set; }

    public IReadOnlyList<NotificationAction> Actions { get; private set; } = [];

    public GroupKey? GroupKey { get; private set; }

    public Guid SourceEventId { get; private set; }

    public string SourceEventType { get; private set; } = null!;

    public string? SourceActionId { get; private set; }

    public NotificationPriority Priority { get; private set; }

    public Guid? SupersededByNotificationId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public DateTimeOffset? SeenAtUtc { get; private set; }

    public DateTimeOffset? SavedAtUtc { get; private set; }

    public DateTimeOffset? DoneAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public DateTimeOffset? SnoozedUntilUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public int OccurrenceCount { get; private set; }

    public DateTimeOffset LastOccurrenceAtUtc { get; private set; }

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
        DateTimeOffset createdAtUtc,
        string? sourceActionId = null,
        NotificationPriority priority = NotificationPriority.PinSystemAdmin,
        string? type = null,
        NotificationActor? actor = null,
        NotificationEntity? entity = null,
        IReadOnlyList<NotificationAction>? actions = null,
        DateTimeOffset? expiresAtUtc = null)
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

        var trimmedSourceActionId = string.IsNullOrWhiteSpace(sourceActionId) ? null : sourceActionId.Trim();
        if (trimmedSourceActionId?.Length > SourceActionIdMaxLength)
        {
            throw new ArgumentException($"Source action id exceeds {SourceActionIdMaxLength} characters.", nameof(sourceActionId));
        }

        var descriptor = string.IsNullOrWhiteSpace(type)
            ? NotificationCatalog.GetByCategory(category)
            : null;
        var notificationType = string.IsNullOrWhiteSpace(type) ? descriptor!.Type : type.Trim();
        if (notificationType.Length > NotificationTypeMaxLength)
        {
            throw new ArgumentException($"Notification type exceeds {NotificationTypeMaxLength} characters.", nameof(type));
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Category = category,
            Severity = severity,
            Type = notificationType,
            Content = content,
            Data = data,
            Actor = actor,
            Entity = entity,
            Actions = NormalizeActions(actions, category, content, descriptor),
            GroupKey = groupKey,
            SourceEventId = sourceEventId,
            SourceEventType = trimmedType,
            SourceActionId = trimmedSourceActionId,
            Priority = priority,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            OccurrenceCount = 1,
            LastOccurrenceAtUtc = createdAtUtc,
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

    public const int NotificationTypeMaxLength = 120;

    public bool MarkRead(DateTimeOffset readAtUtc)
    {
        if (ReadAtUtc.HasValue)
        {
            return false;
        }

        ReadAtUtc = readAtUtc;
        SeenAtUtc ??= readAtUtc;
        _domainEvents.Add(new NotificationReadEvent(Id, RecipientUserId, readAtUtc));
        return true;
    }

    public bool MarkUnread()
    {
        if (!ReadAtUtc.HasValue)
        {
            return false;
        }

        ReadAtUtc = null;
        return true;
    }

    public bool MarkSeen(DateTimeOffset seenAtUtc)
    {
        if (SeenAtUtc.HasValue)
        {
            return false;
        }

        SeenAtUtc = seenAtUtc;
        return true;
    }

    public bool Save(DateTimeOffset savedAtUtc)
    {
        if (SavedAtUtc.HasValue)
        {
            return false;
        }

        SavedAtUtc = savedAtUtc;
        return true;
    }

    public bool Unsave()
    {
        if (!SavedAtUtc.HasValue)
        {
            return false;
        }

        SavedAtUtc = null;
        return true;
    }

    public bool MarkDone(DateTimeOffset doneAtUtc)
    {
        if (DoneAtUtc.HasValue)
        {
            return false;
        }

        DoneAtUtc = doneAtUtc;
        ArchivedAtUtc = null;
        ReadAtUtc ??= doneAtUtc;
        SeenAtUtc ??= doneAtUtc;
        return true;
    }

    public bool Archive(DateTimeOffset archivedAtUtc)
    {
        if (ArchivedAtUtc.HasValue)
        {
            return false;
        }

        ArchivedAtUtc = archivedAtUtc;
        ReadAtUtc ??= archivedAtUtc;
        SeenAtUtc ??= archivedAtUtc;
        return true;
    }

    public bool Restore()
    {
        if (!DoneAtUtc.HasValue && !ArchivedAtUtc.HasValue)
        {
            return false;
        }

        DoneAtUtc = null;
        ArchivedAtUtc = null;
        return true;
    }

    public bool SnoozeUntil(DateTimeOffset snoozedUntilUtc)
    {
        if (SnoozedUntilUtc == snoozedUntilUtc)
        {
            return false;
        }

        SnoozedUntilUtc = snoozedUntilUtc;
        return true;
    }

    public void ApplyIntent(
        NotificationCategory category,
        NotificationSeverity severity,
        string type,
        NotificationContent content,
        NotificationData data,
        NotificationActor? actor,
        NotificationEntity? entity,
        IReadOnlyList<NotificationAction>? actions,
        GroupKey? groupKey,
        NotificationPriority priority,
        Guid sourceEventId,
        string sourceEventType,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var trimmedNotificationType = type.Trim();
        if (trimmedNotificationType.Length > NotificationTypeMaxLength)
        {
            throw new ArgumentException($"Notification type exceeds {NotificationTypeMaxLength} characters.", nameof(type));
        }

        Category = category;
        Severity = severity;
        Type = trimmedNotificationType;
        Content = content;
        Data = data;
        Actor = actor;
        Entity = entity;
        Actions = NormalizeActions(actions, category, content);
        GroupKey = groupKey;
        Priority = priority;
        SourceEventId = sourceEventId;
        SourceEventType = sourceEventType.Trim();
        LastOccurrenceAtUtc = occurredAtUtc;
        DoneAtUtc = null;
        ArchivedAtUtc = null;
        ReadAtUtc = null;
    }

    public void RegisterOccurrence(
        NotificationContent content,
        NotificationData data,
        Guid sourceEventId,
        string sourceEventType,
        DateTimeOffset occurredAtUtc)
    {
        ApplyIntent(
            Category,
            Severity,
            Type,
            content,
            data,
            Actor,
            Entity,
            Actions,
            GroupKey,
            Priority,
            sourceEventId,
            sourceEventType,
            occurredAtUtc);
        OccurrenceCount++;
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

    private static NotificationAction[] NormalizeActions(
        IReadOnlyList<NotificationAction>? actions,
        NotificationCategory category,
        NotificationContent content,
        NotificationDescriptor? descriptor = null)
    {
        var source = actions is null || actions.Count == 0
            ? descriptor?.DefaultActions ?? NotificationCatalog.GetByCategory(category).DefaultActions
            : actions;

        if (string.IsNullOrWhiteSpace(content.DeepLink))
        {
            return source.ToArray();
        }

        return source
            .Select(action =>
                action.Kind == "deep-link" && string.IsNullOrWhiteSpace(action.DeepLink)
                    ? action with { DeepLink = content.DeepLink }
                    : action)
            .ToArray();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
