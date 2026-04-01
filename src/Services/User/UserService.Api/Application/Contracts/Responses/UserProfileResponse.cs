namespace UserService.Api.Application.Contracts.Responses;

public sealed record IdentityResponse(string Name, string Email, string Username);

public sealed record UserProfileResponse(
    Guid UserId,
    IdentityResponse Identity,
    AccountResponse Account,
    PrivacyResponse Privacy,
    NotificationsResponse Notifications,
    SoundVideoResponse SoundVideo);

public sealed record AccountResponse(string? AvatarUrl, string? AboutMe);

public sealed record PrivacyResponse(bool ShowOnlineStatus, bool ShowLastVisitTime);

public sealed record NotificationsResponse(
    bool NewMessages,
    bool NotificationSound,
    bool DisciplineChatMessages,
    bool Mentions);

public sealed record SoundVideoResponse(
    string? PlaybackDeviceId,
    string? RecordingDeviceId,
    string? WebcamDeviceId);
