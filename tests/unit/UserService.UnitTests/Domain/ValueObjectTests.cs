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
    public void NotificationSettingsDefaultShouldBeTrueForAll()
    {
        var settings = NotificationSettings.Default;

        Assert.True(settings.NewMessages);
        Assert.True(settings.NotificationSound);
        Assert.True(settings.DisciplineChatMessages);
        Assert.True(settings.Mentions);
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
