using FluentAssertions;
using Urfu.Link.Services.Notification.Infrastructure.Grpc;
using PresenceGrpc = Urfu.Link.Services.Presence.Grpc;

namespace Urfu.Link.Services.Notification.UnitTests.Infrastructure;

/// <summary>
/// Pure mapping tests for <see cref="GrpcPresenceClient"/>. The notification router
/// suppresses Push for chat categories iff the user is online on the web tab; getting
/// this mapping wrong means either lost notifications (false positive on web) or
/// duplicate noisy push (false negative). Cover every status × platform combo so the
/// rule cannot regress silently.
/// </summary>
public sealed class GrpcPresenceClientTests
{
    [Fact]
    public void IsOnlineOnWeb_returns_true_when_status_is_online_and_web_platform_present()
    {
        var info = new PresenceGrpc.PresenceInfo
        {
            UserId = Guid.NewGuid().ToString(),
            Status = PresenceGrpc.PresenceStatus.Online,
            Platforms = { PresenceGrpc.Platform.Web },
        };

        GrpcPresenceClient.IsOnlineOnWeb(info).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineOnWeb_returns_true_when_web_is_one_of_multiple_platforms()
    {
        var info = new PresenceGrpc.PresenceInfo
        {
            UserId = Guid.NewGuid().ToString(),
            Status = PresenceGrpc.PresenceStatus.Online,
            Platforms = { PresenceGrpc.Platform.Mobile, PresenceGrpc.Platform.Web },
        };

        GrpcPresenceClient.IsOnlineOnWeb(info).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineOnWeb_returns_false_when_user_is_online_on_mobile_only()
    {
        var info = new PresenceGrpc.PresenceInfo
        {
            UserId = Guid.NewGuid().ToString(),
            Status = PresenceGrpc.PresenceStatus.Online,
            Platforms = { PresenceGrpc.Platform.Mobile },
        };

        GrpcPresenceClient.IsOnlineOnWeb(info).Should().BeFalse();
    }

    [Theory]
    [InlineData(PresenceGrpc.PresenceStatus.Offline)]
    [InlineData(PresenceGrpc.PresenceStatus.Away)]
    [InlineData(PresenceGrpc.PresenceStatus.DoNotDisturb)]
    [InlineData(PresenceGrpc.PresenceStatus.Invisible)]
    public void IsOnlineOnWeb_returns_false_when_status_is_not_online(PresenceGrpc.PresenceStatus status)
    {
        var info = new PresenceGrpc.PresenceInfo
        {
            UserId = Guid.NewGuid().ToString(),
            Status = status,
            Platforms = { PresenceGrpc.Platform.Web },
        };

        GrpcPresenceClient.IsOnlineOnWeb(info).Should().BeFalse();
    }

    [Fact]
    public void IsOnlineOnWeb_returns_false_when_user_has_no_active_platforms()
    {
        var info = new PresenceGrpc.PresenceInfo
        {
            UserId = Guid.NewGuid().ToString(),
            Status = PresenceGrpc.PresenceStatus.Online,
        };

        GrpcPresenceClient.IsOnlineOnWeb(info).Should().BeFalse();
    }
}
