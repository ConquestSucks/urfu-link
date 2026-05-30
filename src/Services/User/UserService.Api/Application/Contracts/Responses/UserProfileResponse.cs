namespace UserService.Api.Application.Contracts.Responses;

public sealed record IdentityResponse(string Name, string Email, string Username);

public sealed record UserProfileResponse(
    Guid UserId,
    IdentityResponse Identity,
    AccountResponse Account,
    PrivacyResponse Privacy,
    NotificationsResponse Notifications,
    NotificationPreferencesResponse Preferences,
    SoundVideoResponse SoundVideo);

public sealed record AccountResponse(string? AvatarUrl, string? AboutMe);

public sealed record PrivacyResponse(bool ShowOnlineStatus, bool ShowLastVisitTime);

public sealed record NotificationsResponse(
    bool NewMessages,
    bool NotificationSound,
    bool DisciplineChatMessages,
    bool Mentions,
    IReadOnlyList<string> MutedConversationIds);

public sealed record NotificationPreferencesResponse(
    IReadOnlyDictionary<int, ChannelToggleResponse> Categories,
    QuietHoursResponse QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound,
    IReadOnlyList<string> MutedConversationIds);

public sealed record ChannelToggleResponse(bool Push, bool Email, bool InApp);

public sealed record QuietHoursResponse(string IanaTimezone, string? Start, string? End, bool Enabled);

public sealed record SoundVideoResponse(
    string? PlaybackDeviceId,
    string? RecordingDeviceId,
    string? WebcamDeviceId);
