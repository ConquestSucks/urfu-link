using UserService.Api.Domain.ValueObjects;

namespace UserService.UnitTests.Domain;

public sealed class ValueObjectTests
{
    [Fact]
    public void AccountSettingsDefaultShouldHaveNulls()
    {
        var settings = AccountSettings.Default;

        Assert.Null(settings.AvatarUrl);
        Assert.Null(settings.AboutMe);
    }

    [Fact]
    public void PrivacySettingsDefaultShouldBeTrueForBothFlags()
    {
        var settings = PrivacySettings.Default;

        Assert.True(settings.ShowOnlineStatus);
        Assert.True(settings.ShowLastVisitTime);
    }

    [Fact]
    public void NotificationSettingsDefaultEnablesAllCategoriesOnAllChannels()
    {
        var settings = NotificationSettings.Default;

        Assert.True(settings.Sound);
        Assert.False(settings.DndEnabled);
        Assert.Equal("ru-RU", settings.Locale);
        Assert.False(settings.QuietHours.Enabled);
        Assert.Equal(NotificationCategoryCode.All.Count, settings.Categories.Count);
        foreach (var code in NotificationCategoryCode.All)
        {
            var toggle = settings.GetToggle(code);
            Assert.True(toggle.Push);
            Assert.True(toggle.Email);
            Assert.True(toggle.InApp);
        }
    }

    [Fact]
    public void NotificationSettingsFromLegacyMapsBooleansToCategoryToggles()
    {
        var settings = NotificationSettings.FromLegacy(
            newMessages: true,
            sound: false,
            disciplineChatMessages: false,
            mentions: true);

        Assert.False(settings.Sound);
        Assert.True(settings.GetToggle(NotificationCategoryCode.ChatMessageDirect).Push);
        Assert.False(settings.GetToggle(NotificationCategoryCode.ChatMessageDiscipline).Push);
        Assert.True(settings.GetToggle(NotificationCategoryCode.ChatMessageMention).Push);
        Assert.True(settings.GetToggle(NotificationCategoryCode.CallIncoming).Push);
    }

    [Fact]
    public void NotificationSettingsWithCategoryReturnsNewInstance()
    {
        var original = NotificationSettings.Default;
        var updated = original.WithCategory(NotificationCategoryCode.SystemUpdate, ChannelToggle.AllOff);

        Assert.NotSame(original, updated);
        Assert.True(original.GetToggle(NotificationCategoryCode.SystemUpdate).Push);
        Assert.False(updated.GetToggle(NotificationCategoryCode.SystemUpdate).Push);
    }

    [Fact]
    public void QuietHoursCreateValidatesTimezoneAndDistinctBoundaries()
    {
        var quiet = QuietHours.Create("Asia/Yekaterinburg", new TimeOnly(22, 0), new TimeOnly(8, 0));

        Assert.True(quiet.Enabled);
        Assert.Equal(new TimeOnly(22, 0), quiet.Start);

        Assert.Throws<ArgumentException>(() =>
            QuietHours.Create("Asia/Yekaterinburg", new TimeOnly(22, 0), new TimeOnly(22, 0)));
        Assert.Throws<ArgumentException>(() =>
            QuietHours.Create("Not/A_Real_Zone", new TimeOnly(1, 0), new TimeOnly(2, 0)));
    }

    [Fact]
    public void SoundVideoSettingsDefaultShouldHaveNulls()
    {
        var settings = SoundVideoSettings.Default;

        Assert.Null(settings.PlaybackDeviceId);
        Assert.Null(settings.RecordingDeviceId);
        Assert.Null(settings.WebcamDeviceId);
    }

    [Fact]
    public void ValueObjectsWithSameValuesShouldBeEqual()
    {
        var a = new PrivacySettings(true, false);
        var b = new PrivacySettings(true, false);

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValueObjectsWithDifferentValuesShouldNotBeEqual()
    {
        var a = new PrivacySettings(true, false);
        var b = new PrivacySettings(false, false);

        Assert.NotEqual(a, b);
    }
}
