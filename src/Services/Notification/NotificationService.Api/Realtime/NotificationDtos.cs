using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Realtime;

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientUserId,
    string Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Body,
    string? ImageUrl,
    string? DeepLink,
    IReadOnlyDictionary<string, string> Data,
    NotificationActorDto? Actor,
    NotificationEntityDto? Entity,
    IReadOnlyList<NotificationActionDto> Actions,
    string? GroupKey,
    int OccurrenceCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastOccurrenceAtUtc,
    DateTimeOffset? ReadAtUtc,
    DateTimeOffset? SeenAtUtc,
    DateTimeOffset? SavedAtUtc,
    DateTimeOffset? DoneAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    DateTimeOffset? SnoozedUntilUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? SourceActionId = null,
    NotificationPriority? Priority = null,
    Guid? SupersededByNotificationId = null);

public sealed record NotificationActorDto(Guid? Id, string? DisplayName, string? AvatarUrl);

public sealed record NotificationEntityDto(string Kind, string Id, string? DisplayName);

public sealed record NotificationActionDto(string Id, string Label, string Kind, string? DeepLink);

public sealed record BadgeSnapshotDto(
    int Total,
    IReadOnlyDictionary<int, int> PerCategory,
    int TotalUnseen = 0,
    int UrgentUnread = 0,
    IReadOnlyDictionary<string, int>? PerType = null);
