using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Routing;

public sealed record NotificationIntent(
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationSeverity Severity,
    NotificationContent Content,
    NotificationData Data,
    GroupKey? GroupKey,
    Guid SourceEventId,
    string SourceEventType,
    string? SourceActionId = null,
    NotificationPriority Priority = NotificationPriority.PinSystemAdmin,
    string? Type = null,
    NotificationActor? Actor = null,
    NotificationEntity? Entity = null,
    IReadOnlyList<NotificationAction>? Actions = null,
    string? SuppressWhenViewingContextKey = null);
