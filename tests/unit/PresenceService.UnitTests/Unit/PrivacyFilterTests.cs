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
    public void Apply_AlwaysPassesStatusThrough()
    {
        var settings = new PrivacySettings(ShowLastVisitTime: true);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.Status.Should().Be(PresenceStatus.Online);
    }

    [Fact]
    public void Apply_AlwaysPassesPlatformsThrough()
    {
        var settings = new PrivacySettings(ShowLastVisitTime: true);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.Platforms.Should().Equal(Platform.Web, Platform.Mobile);
    }

    [Fact]
    public void Apply_ShowLastVisitTrue_ClearsLastSeen()
    {
        var settings = new PrivacySettings(ShowLastVisitTime: true);
        var result = PrivacyFilter.Apply(Online(), settings);

        result.LastSeenAt.Should().BeNull();
        result.Status.Should().Be(PresenceStatus.Online);
    }

    [Fact]
    public void Apply_ShowLastVisitFalse_PassesThrough()
    {
        var settings = new PrivacySettings(ShowLastVisitTime: false);
        var input = Online();
        var result = PrivacyFilter.Apply(input, settings);

        result.Should().Be(input);
    }

    [Fact]
    public void Apply_DefaultSettings_HidesLastSeenOnly()
    {
        var input = Online();
        var result = PrivacyFilter.Apply(input, PrivacySettings.Default);

        result.LastSeenAt.Should().BeNull();
        result.Status.Should().Be(input.Status);
        result.Platforms.Should().Equal(input.Platforms);
    }
}
