using FluentAssertions;
using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.UnitTests.Unit;

public class PresenceAggregatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly PresenceAggregator _sut = new();

    private static PresenceSession Session(Platform platform, PresenceStatus status, string deviceId = "d1")
        => new(UserId, deviceId, platform, status, CustomActivity: null, ConnectedAt: Now, LastHeartbeatAt: Now);

    [Fact]
    public void Aggregate_NoSessions_ReturnsOffline()
    {
        var lastSeen = Now.AddMinutes(-5);
        var result = _sut.Aggregate(UserId, sessions: [], lastSeenAt: lastSeen);

        result.UserId.Should().Be(UserId);
        result.Status.Should().Be(PresenceStatus.Offline);
        result.Platforms.Should().BeEmpty();
        result.LastSeenAt.Should().Be(lastSeen);
    }

    [Fact]
    public void Aggregate_OneOnlineSession_ReturnsOnline()
    {
        var result = _sut.Aggregate(UserId, [Session(Platform.Web, PresenceStatus.Online)], lastSeenAt: null);

        result.Status.Should().Be(PresenceStatus.Online);
        result.Platforms.Should().Equal(Platform.Web);
    }

    [Fact]
    public void Aggregate_OnlineAndAway_PrioritizesOnline()
    {
        var result = _sut.Aggregate(UserId,
            [
                Session(Platform.Web, PresenceStatus.Away, deviceId: "d1"),
                Session(Platform.Mobile, PresenceStatus.Online, deviceId: "d2"),
            ],
            lastSeenAt: null);

        result.Status.Should().Be(PresenceStatus.Online);
    }

    [Fact]
    public void Aggregate_AllAway_ReturnsAway()
    {
        var result = _sut.Aggregate(UserId,
            [
                Session(Platform.Web, PresenceStatus.Away, "d1"),
                Session(Platform.Mobile, PresenceStatus.Away, "d2"),
            ],
            lastSeenAt: null);

        result.Status.Should().Be(PresenceStatus.Away);
    }

    [Fact]
    public void Aggregate_OnlyDoNotDisturb_ReturnsDoNotDisturb()
    {
        var result = _sut.Aggregate(UserId,
            [Session(Platform.Desktop, PresenceStatus.DoNotDisturb)],
            lastSeenAt: null);

        result.Status.Should().Be(PresenceStatus.DoNotDisturb);
    }

    [Fact]
    public void Aggregate_DistinctPlatforms_ReturnsUnion()
    {
        var result = _sut.Aggregate(UserId,
            [
                Session(Platform.Web, PresenceStatus.Online, "d1"),
                Session(Platform.Mobile, PresenceStatus.Online, "d2"),
                Session(Platform.Desktop, PresenceStatus.Away, "d3"),
            ],
            lastSeenAt: null);

        result.Platforms.Should().BeEquivalentTo(new[] { Platform.Mobile, Platform.Web, Platform.Desktop });
    }

    [Fact]
    public void Aggregate_DuplicatePlatforms_AreDeduplicated()
    {
        var result = _sut.Aggregate(UserId,
            [
                Session(Platform.Web, PresenceStatus.Online, "d1"),
                Session(Platform.Web, PresenceStatus.Online, "d2"),
            ],
            lastSeenAt: null);

        result.Platforms.Should().Equal(Platform.Web);
    }

    [Fact]
    public void Aggregate_PassesLastSeenAtThrough()
    {
        var ts = Now.AddHours(-1);
        var result = _sut.Aggregate(UserId,
            [Session(Platform.Web, PresenceStatus.Online)],
            lastSeenAt: ts);

        result.LastSeenAt.Should().Be(ts);
    }
}
