namespace Urfu.Link.Services.Notification.Domain.ValueObjects;

public sealed record NotificationActor(
    Guid? Id,
    string? DisplayName,
    string? AvatarUrl);

public sealed record NotificationEntity(
    string Kind,
    string Id,
    string? DisplayName);

public sealed record NotificationAction(
    string Id,
    string Label,
    string Kind,
    string? DeepLink);
