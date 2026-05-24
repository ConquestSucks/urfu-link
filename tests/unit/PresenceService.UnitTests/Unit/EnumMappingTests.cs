using FluentAssertions;
using Urfu.Link.Services.Presence.Domain.Enums;

namespace PresenceService.UnitTests.Unit;

public class EnumMappingTests
{
    [Theory]
    [InlineData(PresenceStatus.Online, 0)]
    [InlineData(PresenceStatus.Away, 1)]
    [InlineData(PresenceStatus.DoNotDisturb, 2)]
    [InlineData(PresenceStatus.Invisible, 3)]
    [InlineData(PresenceStatus.Offline, 4)]
    public void PresenceStatus_HasStableNumericValue(PresenceStatus status, int expected)
    {
        ((int)status).Should().Be(expected,
            "presence status numeric values are part of the gRPC and Kafka contract; do not reorder");
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Theory]
    [InlineData(Platform.Mobile, 0)]
    [InlineData(Platform.Web, 1)]
    [InlineData(Platform.Desktop, 2)]
    public void Platform_HasStableNumericValue(Platform platform, int expected)
    {
        ((int)platform).Should().Be(expected,
            "platform numeric values are part of the gRPC and Kafka contract; do not reorder");
        Enum.IsDefined(platform).Should().BeTrue();
    }

    [Fact]
    public void PresenceStatus_AllValuesAreCovered()
    {
        Enum.GetValues<PresenceStatus>().Should().BeEquivalentTo(
            new[]
            {
                PresenceStatus.Online,
                PresenceStatus.Away,
                PresenceStatus.DoNotDisturb,
                PresenceStatus.Invisible,
                PresenceStatus.Offline,
            });
    }

    [Fact]
    public void Platform_AllValuesAreCovered()
    {
        Enum.GetValues<Platform>().Should().BeEquivalentTo(
            new[] { Platform.Mobile, Platform.Web, Platform.Desktop });
    }
}
