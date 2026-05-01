using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Routing;

public sealed record NotificationDraft(
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationSeverity Severity,
    NotificationContent Content,
    NotificationData Data,
    GroupKey? GroupKey,
    Guid SourceEventId,
    string SourceEventType);
