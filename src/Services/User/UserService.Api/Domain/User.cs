using Urfu.Link.BuildingBlocks.Contracts.Integration;
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

    public void UpdateNotifications(
        bool newMessages,
        bool notificationSound,
        bool disciplineChatMessages,
        bool mentions)
    {
        Notifications = new NotificationSettings(newMessages, notificationSound, disciplineChatMessages, mentions);
        Touch();
        _domainEvents.Add(new UserNotificationSettingsChangedEvent(
            Id, newMessages, notificationSound, disciplineChatMessages, mentions));
    }

    public void UpdateSoundVideo(string? playbackDeviceId, string? recordingDeviceId, string? webcamDeviceId)
    {
        SoundVideo = new SoundVideoSettings(playbackDeviceId, recordingDeviceId, webcamDeviceId);
        Touch();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}
