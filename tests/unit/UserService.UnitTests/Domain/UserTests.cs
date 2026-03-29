using UserService.Api.Domain;
using UserService.Api.Domain.Events;
using UserService.Api.Domain.ValueObjects;

namespace UserService.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void CreateDefaultShouldSetIdAndDefaultSettings()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        Assert.Equal(TestUserId, user.Id);
        Assert.Null(user.Account.AvatarUrl);
        Assert.Null(user.Account.AboutMe);
        Assert.True(user.Privacy.ShowOnlineStatus);
        Assert.True(user.Privacy.ShowLastVisitTime);
        Assert.True(user.Notifications.NewMessages);
        Assert.True(user.Notifications.NotificationSound);
        Assert.True(user.Notifications.DisciplineChatMessages);
        Assert.True(user.Notifications.Mentions);
        Assert.Null(user.SoundVideo.PlaybackDeviceId);
        Assert.Null(user.SoundVideo.RecordingDeviceId);
        Assert.Null(user.SoundVideo.WebcamDeviceId);
    }

    [Fact]
    public void CreateDefaultShouldSetTimestamps()
    {
        var before = DateTimeOffset.UtcNow;
        var user = UserProfile.CreateDefault(TestUserId);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(user.CreatedAtUtc, before, after);
        Assert.InRange(user.UpdatedAtUtc, before, after);
    }

    [Fact]
    public void CreateDefaultShouldHaveNoDomainEvents()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void UpdateAccountShouldChangeAboutMeAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        user.UpdateAccount("Hello, I'm a student");

        Assert.Equal("Hello, I'm a student", user.Account.AboutMe);
        var evt = Assert.Single(user.DomainEvents);
        var profileEvent = Assert.IsType<UserProfileUpdatedEvent>(evt);
        Assert.Equal(TestUserId, profileEvent.UserId);
        Assert.Equal("Hello, I'm a student", profileEvent.AboutMe);
    }

    [Fact]
    public void UpdateAccountWithNullShouldClearAboutMe()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        user.UpdateAccount("Something");

        user.ClearDomainEvents();
        user.UpdateAccount(null);

        Assert.Null(user.Account.AboutMe);
        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void UpdateAccountShouldUpdateTimestamp()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        var originalUpdated = user.UpdatedAtUtc;

        user.UpdateAccount("New bio");

        Assert.True(user.UpdatedAtUtc >= originalUpdated);
    }

    [Fact]
    public void UploadAvatarShouldSetUrlAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        const string avatarUrl = "http://minio:9000/user-avatars/avatars/test/img.jpg";

        user.UploadAvatar(avatarUrl);

        Assert.Equal(avatarUrl, user.Account.AvatarUrl);
        var evt = Assert.Single(user.DomainEvents);
        var avatarEvent = Assert.IsType<UserAvatarChangedEvent>(evt);
        Assert.Equal(TestUserId, avatarEvent.UserId);
        Assert.Equal(avatarUrl, avatarEvent.AvatarUrl);
    }

    [Fact]
    public void RemoveAvatarShouldClearUrlAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        user.UploadAvatar("http://minio:9000/some-avatar.jpg");
        user.ClearDomainEvents();

        user.RemoveAvatar();

        Assert.Null(user.Account.AvatarUrl);
        var evt = Assert.Single(user.DomainEvents);
        var avatarEvent = Assert.IsType<UserAvatarChangedEvent>(evt);
        Assert.Null(avatarEvent.AvatarUrl);
    }

    [Fact]
    public void UpdatePrivacyShouldChangeSettingsAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        user.UpdatePrivacy(showOnlineStatus: false, showLastVisitTime: false);

        Assert.False(user.Privacy.ShowOnlineStatus);
        Assert.False(user.Privacy.ShowLastVisitTime);
        var evt = Assert.Single(user.DomainEvents);
        var privacyEvent = Assert.IsType<UserPrivacySettingsChangedEvent>(evt);
        Assert.Equal(TestUserId, privacyEvent.UserId);
        Assert.False(privacyEvent.ShowOnlineStatus);
        Assert.False(privacyEvent.ShowLastVisitTime);
    }

    [Fact]
    public void UpdateNotificationsShouldChangeAllFlagsAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        user.UpdateNotifications(
            newMessages: false,
            notificationSound: false,
            disciplineChatMessages: true,
            mentions: false);

        Assert.False(user.Notifications.NewMessages);
        Assert.False(user.Notifications.NotificationSound);
        Assert.True(user.Notifications.DisciplineChatMessages);
        Assert.False(user.Notifications.Mentions);
        var evt = Assert.Single(user.DomainEvents);
        var notifEvent = Assert.IsType<UserNotificationSettingsChangedEvent>(evt);
        Assert.Equal(TestUserId, notifEvent.UserId);
        Assert.False(notifEvent.NewMessages);
        Assert.False(notifEvent.NotificationSound);
        Assert.True(notifEvent.DisciplineChatMessages);
        Assert.False(notifEvent.Mentions);
    }

    [Fact]
    public void UpdateSoundVideoShouldChangeDeviceIdsWithNoEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        user.UpdateSoundVideo("speaker-1", "mic-1", "cam-1");

        Assert.Equal("speaker-1", user.SoundVideo.PlaybackDeviceId);
        Assert.Equal("mic-1", user.SoundVideo.RecordingDeviceId);
        Assert.Equal("cam-1", user.SoundVideo.WebcamDeviceId);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void ClearDomainEventsShouldRemoveAllEvents()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        user.UpdateAccount("bio");
        user.UpdatePrivacy(false, false);
        Assert.Equal(2, user.DomainEvents.Count);

        user.ClearDomainEvents();

        Assert.Empty(user.DomainEvents);
    }
}
