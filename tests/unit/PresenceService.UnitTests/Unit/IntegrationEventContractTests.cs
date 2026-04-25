using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;

namespace PresenceService.UnitTests.Unit;

public class IntegrationEventContractTests
{
    [Fact]
    public void UserCameOnlineEvent_HasFrozenEventType()
    {
        var ev = new UserCameOnlineEvent(
            UserId: Guid.NewGuid(),
            Platforms: new[] { Platform.Web },
            OccurredAtUtc: DateTimeOffset.UtcNow);

        ev.EventType.Should().Be("presence.user.online.v1",
            "EventType is part of the Kafka contract — consumers (notification, web) parse on this value");
        ev.Should().BeAssignableTo<IIntegrationEvent>();
        ev.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UserWentOfflineEvent_HasFrozenEventType()
    {
        var ev = new UserWentOfflineEvent(
            UserId: Guid.NewGuid(),
            LastSeenAt: DateTimeOffset.UtcNow,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        ev.EventType.Should().Be("presence.user.offline.v1");
        ev.Should().BeAssignableTo<IIntegrationEvent>();
        ev.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UserCameOnlineEvent_AssignsUniqueEventIds()
    {
        var first = new UserCameOnlineEvent(Guid.NewGuid(), [Platform.Web], DateTimeOffset.UtcNow);
        var second = new UserCameOnlineEvent(Guid.NewGuid(), [Platform.Web], DateTimeOffset.UtcNow);
        first.EventId.Should().NotBe(second.EventId);
    }
}
