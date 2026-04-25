using FluentAssertions;
using Urfu.Link.Services.Presence.Application.Privacy;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.UnitTests.Unit;

public class PrivacyFilterTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset LastSeen = DateTimeOffset.UtcNow.AddMinutes(-1);

    private static AggregatedPresence Online() => new(
        UserId,
        PresenceStatus.Online,
        new[] { Platform.Web, Platform.Mobile },
        LastSeen);

    [Fact]
    public void Apply_ShowOnlineFalse_ReturnsOffline()
    {
        var settings = new PrivacySettings(ShowOnlineStatus: false, ShowLastVisitTime: true);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.Status.Should().Be(PresenceStatus.Offline);
    }

    [Fact]
    public void Apply_ShowOnlineFalse_ClearsPlatforms()
    {
        var settings = new PrivacySettings(ShowOnlineStatus: false, ShowLastVisitTime: true);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.Platforms.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ShowLastVisitFalse_ClearsLastSeen()
    {
        var settings = new PrivacySettings(ShowOnlineStatus: true, ShowLastVisitTime: false);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.LastSeenAt.Should().BeNull();
        result.Status.Should().Be(PresenceStatus.Online);
    }

    [Fact]
    public void Apply_AllAllowed_PassesThrough()
    {
        var settings = new PrivacySettings(ShowOnlineStatus: true, ShowLastVisitTime: true);
        var input = Online();
        var result = PrivacyFilter.Apply(input, settings);

        result.Should().Be(input);
    }

    [Fact]
    public void Apply_DefaultSettings_AllowsEverything()
    {
        var input = Online();
        var result = PrivacyFilter.Apply(input, PrivacySettings.Default);

        result.Should().Be(input);
    }

    [Fact]
    public void Apply_BothHidden_StatusOfflineAndLastSeenNull()
    {
        var settings = new PrivacySettings(ShowOnlineStatus: false, ShowLastVisitTime: false);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.Status.Should().Be(PresenceStatus.Offline);
        result.Platforms.Should().BeEmpty();
        result.LastSeenAt.Should().BeNull();
    }
}
