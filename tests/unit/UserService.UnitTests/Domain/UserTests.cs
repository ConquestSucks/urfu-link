using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
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
        Assert.True(user.Notifications.Sound);
        Assert.False(user.Notifications.DndEnabled);
        Assert.Equal("ru-RU", user.Notifications.Locale);
        Assert.False(user.Notifications.QuietHours.Enabled);
        Assert.True(user.Notifications.GetToggle(NotificationCategoryCode.ChatMessageDirect).Push);
        Assert.Equal(NotificationCategoryCode.All.Count, user.Notifications.Categories.Count);
        Assert.Empty(user.Notifications.MutedConversationIds);
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
    public void UpdateNotificationsShouldMapLegacyFlagsAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        user.UpdateNotificationPreferences(
            NotificationSettings.Default
                .WithDnd(true)
                .WithLocale("en-US")
                .WithMutedConversation("direct:abc"));
        user.ClearDomainEvents();

        user.UpdateNotifications(
            newMessages: false,
            notificationSound: false,
            disciplineChatMessages: true,
            mentions: false);

        Assert.False(user.Notifications.Sound);
        Assert.False(user.Notifications.GetToggle(NotificationCategoryCode.ChatMessageDirect).Push);
        Assert.True(user.Notifications.GetToggle(NotificationCategoryCode.ChatMessageDiscipline).Push);
        Assert.False(user.Notifications.GetToggle(NotificationCategoryCode.ChatMessageMention).Push);
        Assert.True(user.Notifications.DndEnabled);
        Assert.Equal("en-US", user.Notifications.Locale);
        Assert.Contains("direct:abc", user.Notifications.MutedConversationIds);

        var evt = Assert.Single(user.DomainEvents);
        var notifEvent = Assert.IsType<UserNotificationSettingsChangedEvent>(evt);
        Assert.Equal(TestUserId, notifEvent.UserId);
        Assert.Equal("en-US", notifEvent.Preferences.Locale);
        Assert.True(notifEvent.Preferences.DndEnabled);
        Assert.False(notifEvent.Preferences.Categories[NotificationCategoryCode.ChatMessageDirect].Push);
        Assert.True(notifEvent.Preferences.Categories[NotificationCategoryCode.ChatMessageDiscipline].Push);
        Assert.Contains("direct:abc", notifEvent.Preferences.MutedConversationIds ?? []);
    }

    [Fact]
    public void UpdateNotificationPreferencesReplacesEntireSettings()
    {
        var user = UserProfile.CreateDefault(TestUserId);
        var custom = NotificationSettings.Default
            .WithCategory(NotificationCategoryCode.CallIncoming, ChannelToggle.AllOff)
            .WithDnd(true)
            .WithLocale("en-US");

        user.UpdateNotificationPreferences(custom);

        Assert.True(user.Notifications.DndEnabled);
        Assert.Equal("en-US", user.Notifications.Locale);
        Assert.False(user.Notifications.GetToggle(NotificationCategoryCode.CallIncoming).Push);

        var evt = Assert.Single(user.DomainEvents);
        var notifEvent = Assert.IsType<UserNotificationSettingsChangedEvent>(evt);
        Assert.Equal("en-US", notifEvent.Preferences.Locale);
        Assert.True(notifEvent.Preferences.DndEnabled);
    }

    [Fact]
    public void MuteAndUnmuteConversationShouldUpdatePreferencesAndRaiseEvent()
    {
        var user = UserProfile.CreateDefault(TestUserId);

        user.MuteConversationNotifications(" direct:abc ");

        Assert.Contains("direct:abc", user.Notifications.MutedConversationIds);
        var muteEvent = Assert.IsType<UserNotificationSettingsChangedEvent>(
            Assert.Single(user.DomainEvents));
        Assert.Contains("direct:abc", muteEvent.Preferences.MutedConversationIds ?? []);

        user.ClearDomainEvents();
        user.UnmuteConversationNotifications("direct:abc");

        Assert.DoesNotContain("direct:abc", user.Notifications.MutedConversationIds);
        var unmuteEvent = Assert.IsType<UserNotificationSettingsChangedEvent>(
            Assert.Single(user.DomainEvents));
        Assert.DoesNotContain("direct:abc", unmuteEvent.Preferences.MutedConversationIds ?? []);
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
