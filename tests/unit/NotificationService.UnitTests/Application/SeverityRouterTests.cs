using FluentAssertions;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Application;

public sealed class SeverityRouterTests
{
    [Fact]
    public void Low_OnlyInApp()
    {
        var channels = SeverityRouter.Select(NotificationSeverity.Low);
        channels.Should().BeEquivalentTo([DeliveryChannel.InApp]);
    }

    [Fact]
    public void Normal_InAppAndPush()
    {
        var channels = SeverityRouter.Select(NotificationSeverity.Normal);
        channels.Should().BeEquivalentTo([DeliveryChannel.InApp, DeliveryChannel.Push]);
    }

    [Fact]
    public void High_AddsEmailFallback()
    {
        var channels = SeverityRouter.Select(NotificationSeverity.High);
        channels.Should().Contain(DeliveryChannel.Email);
    }

    [Fact]
    public void Urgent_IncludesAllChannels()
    {
        var channels = SeverityRouter.Select(NotificationSeverity.Urgent);
        channels.Should().Contain([DeliveryChannel.InApp, DeliveryChannel.Push, DeliveryChannel.Email]);
    }
}
