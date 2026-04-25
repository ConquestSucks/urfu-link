using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;

namespace PresenceService.UnitTests.Unit;

public class LastSeenTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_RecordsInitialFields()
    {
        var ts = DateTimeOffset.UtcNow;
        var ls = LastSeen.Create(UserId, Platform.Mobile, ts);

        ls.UserId.Should().Be(UserId);
        ls.LastPlatform.Should().Be(Platform.Mobile);
        ls.LastSeenAt.Should().Be(ts);
        ls.DomainEvents.Should().BeEmpty(
            "LastSeen.Create represents an initial seed; events should fire only on Update");
    }

    [Fact]
    public void Update_AppendsUserWentOfflineEvent()
    {
        var ls = LastSeen.Create(UserId, Platform.Web, DateTimeOffset.UtcNow.AddMinutes(-5));
        var newTs = DateTimeOffset.UtcNow;

        ls.Update(Platform.Desktop, newTs);

        ls.DomainEvents.Should().HaveCount(1);
        var ev = ls.DomainEvents.Single().Should().BeOfType<UserWentOfflineEvent>().Subject;
        ev.UserId.Should().Be(UserId);
        ev.LastSeenAt.Should().Be(newTs);
    }

    [Fact]
    public void Update_OverwritesLastSeenAtAndPlatform()
    {
        var ls = LastSeen.Create(UserId, Platform.Web, DateTimeOffset.UtcNow.AddMinutes(-10));
        var newTs = DateTimeOffset.UtcNow;

        ls.Update(Platform.Mobile, newTs);

        ls.LastSeenAt.Should().Be(newTs);
        ls.LastPlatform.Should().Be(Platform.Mobile);
    }

    [Fact]
    public void ClearDomainEvents_Empties()
    {
        var ls = LastSeen.Create(UserId, Platform.Web, DateTimeOffset.UtcNow.AddMinutes(-10));
        ls.Update(Platform.Mobile, DateTimeOffset.UtcNow);
        ls.DomainEvents.Should().NotBeEmpty();

        ls.ClearDomainEvents();

        ls.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_AreReadOnly()
    {
        var ls = LastSeen.Create(UserId, Platform.Web, DateTimeOffset.UtcNow);
        ls.DomainEvents.Should().BeAssignableTo<IReadOnlyList<IIntegrationEvent>>();
    }
}
