using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
using UserService.Api.Domain.Events;
using UserService.Api.Domain.ValueObjects;

namespace UserService.Api.Domain;

public sealed class UserProfile
{
    private readonly List<IIntegrationEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public AccountSettings Account { get; private set; } = AccountSettings.Default;
    public PrivacySettings Privacy { get; private set; } = PrivacySettings.Default;
    public NotificationSettings Notifications { get; private set; } = NotificationSettings.Default;
    public SoundVideoSettings SoundVideo { get; private set; } = SoundVideoSettings.Default;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<IIntegrationEvent> DomainEvents => _domainEvents;

    private UserProfile() { }

    public static UserProfile CreateDefault(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserProfile
        {
            Id = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void UpdateAccount(string? aboutMe)
    {
        Account = Account with { AboutMe = aboutMe };
        Touch();
        _domainEvents.Add(new UserProfileUpdatedEvent(Id, aboutMe));
    }

    public void UploadAvatar(string avatarUrl)
    {
        Account = Account with { AvatarUrl = avatarUrl };
        Touch();
        _domainEvents.Add(new UserAvatarChangedEvent(Id, avatarUrl));
    }

    public void RemoveAvatar()
    {
        Account = Account with { AvatarUrl = null };
        Touch();
        _domainEvents.Add(new UserAvatarChangedEvent(Id, null));
    }

    public void UpdatePrivacy(bool showOnlineStatus, bool showLastVisitTime)
    {
        Privacy = new PrivacySettings(showOnlineStatus, showLastVisitTime);
        Touch();
        _domainEvents.Add(new UserPrivacySettingsChangedEvent(Id, showOnlineStatus, showLastVisitTime));
    }

    /// <summary>
    /// Legacy entry point used by <c>PUT /me/notifications</c> with four boolean toggles.
    /// Maps onto the modern per-category structure for backwards compatibility.
    /// </summary>
    public void UpdateNotifications(
        bool newMessages,
        bool notificationSound,
        bool disciplineChatMessages,
        bool mentions)
    {
        Notifications = NotificationSettings.FromLegacy(newMessages, notificationSound, disciplineChatMessages, mentions);
        Touch();
        _domainEvents.Add(BuildSettingsChangedEvent());
    }

    /// <summary>
    /// Replace the entire notification preferences set (per-category, quiet hours, DND, locale).
    /// </summary>
    public void UpdateNotificationPreferences(NotificationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Notifications = settings;
        Touch();
        _domainEvents.Add(BuildSettingsChangedEvent());
    }

    public void UpdateSoundVideo(string? playbackDeviceId, string? recordingDeviceId, string? webcamDeviceId)
    {
        SoundVideo = new SoundVideoSettings(playbackDeviceId, recordingDeviceId, webcamDeviceId);
        Touch();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private UserNotificationSettingsChangedEvent BuildSettingsChangedEvent()
    {
        var categories = Notifications.Categories.ToDictionary(
            kv => kv.Key,
            kv => new ChannelTogglePayload(kv.Value.Push, kv.Value.Email, kv.Value.InApp));

        var quietHours = new QuietHoursPayload(
            Notifications.QuietHours.IanaTimezone,
            Notifications.QuietHours.Start?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Notifications.QuietHours.End?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Notifications.QuietHours.Enabled);

        var preferences = new NotificationPreferencesPayload(
            categories,
            quietHours,
            Notifications.DndEnabled,
            Notifications.Locale);

        return new UserNotificationSettingsChangedEvent(Id, preferences);
    }

    private void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}
