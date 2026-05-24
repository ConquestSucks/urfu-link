using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Realtime;

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Body,
    string? ImageUrl,
    string? DeepLink,
    IReadOnlyDictionary<string, string> Data,
    string? GroupKey,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed record BadgeSnapshotDto(int Total, IReadOnlyDictionary<int, int> PerCategory);
