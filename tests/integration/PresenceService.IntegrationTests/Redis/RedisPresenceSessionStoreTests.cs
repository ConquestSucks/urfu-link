using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.IntegrationTests.Redis;

[Collection(IntegrationCollection.Name)]
public class RedisPresenceSessionStoreTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public RedisPresenceSessionStoreTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private IPresenceSessionStore Resolve() =>
        _factory.Services.GetRequiredService<IPresenceSessionStore>();

    private static PresenceSession Session(Guid userId, string deviceId, Platform platform = Platform.Web)
        => new(userId, deviceId, platform, PresenceStatus.Online,
            CustomActivity: null, ConnectedAt: DateTimeOffset.UtcNow, LastHeartbeatAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddSession_FirstSession_ReturnsTrue()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();

        var wasFirst = await sut.AddSessionAsync(Session(userId, "d1"), CancellationToken.None);

        wasFirst.Should().BeTrue();
    }

    [Fact]
    public async Task AddSession_SecondSession_ReturnsFalse()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.AddSessionAsync(Session(userId, "d1"), CancellationToken.None);

        var wasFirst = await sut.AddSessionAsync(Session(userId, "d2", Platform.Mobile), CancellationToken.None);

        wasFirst.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveSession_LastSession_ReturnsRemovedAndWasLast()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.AddSessionAsync(Session(userId, "d1"), CancellationToken.None);

        var (removed, wasLast) = await sut.RemoveSessionAsync(userId, "d1", CancellationToken.None);

        removed.Should().BeTrue();
        wasLast.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveSession_AlreadyGone_ReturnsRemovedFalseWasLastFalse()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();

        var (removed, wasLast) = await sut.RemoveSessionAsync(userId, "missing", CancellationToken.None);

        removed.Should().BeFalse();
        wasLast.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveSession_OneOfMany_RemovedTrueWasLastFalse()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.AddSessionAsync(Session(userId, "d1"), CancellationToken.None);
        await sut.AddSessionAsync(Session(userId, "d2", Platform.Mobile), CancellationToken.None);

        var (removed, wasLast) = await sut.RemoveSessionAsync(userId, "d1", CancellationToken.None);

        removed.Should().BeTrue();
        wasLast.Should().BeFalse();
    }

    [Fact]
    public async Task GetSessions_ReturnsAllDevices()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.AddSessionAsync(Session(userId, "d1", Platform.Web), CancellationToken.None);
        await sut.AddSessionAsync(Session(userId, "d2", Platform.Mobile), CancellationToken.None);
        await sut.AddSessionAsync(Session(userId, "d3", Platform.Desktop), CancellationToken.None);

        var sessions = await sut.GetSessionsAsync(userId, CancellationToken.None);

        sessions.Should().HaveCount(3);
        sessions.Select(s => s.DeviceId).Should().BeEquivalentTo(new[] { "d1", "d2", "d3" });
        sessions.Select(s => s.Platform).Should().BeEquivalentTo(
            new[] { Platform.Web, Platform.Mobile, Platform.Desktop });
    }

    [Fact]
    public async Task RefreshHeartbeat_UpdatesLastHeartbeatAtAndScore()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.AddSessionAsync(Session(userId, "d1"), CancellationToken.None);
        var newTs = DateTimeOffset.UtcNow.AddSeconds(20);

        await sut.RefreshHeartbeatAsync(userId, "d1", newTs, CancellationToken.None);

        var sessions = await sut.GetSessionsAsync(userId, CancellationToken.None);
        sessions.Single().LastHeartbeatAt.Should().BeCloseTo(newTs, TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task UpdateSessionStatus_PersistsStatusAndCustomActivity()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.AddSessionAsync(Session(userId, "d1"), CancellationToken.None);

        await sut.UpdateSessionStatusAsync(
            userId, "d1", PresenceStatus.DoNotDisturb, "Focusing", CancellationToken.None);

        var session = (await sut.GetSessionsAsync(userId, CancellationToken.None)).Single();
        session.Status.Should().Be(PresenceStatus.DoNotDisturb);
        session.CustomActivity.Should().Be("Focusing");
    }

    [Fact]
    public async Task GetExpiredSessions_ReturnsOnlyOldOnes()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-1);
        var fresh = DateTimeOffset.UtcNow;

        var session1 = Session(userId, "d1") with { LastHeartbeatAt = stale };
        var session2 = Session(userId, "d2", Platform.Mobile) with { LastHeartbeatAt = fresh };
        await sut.AddSessionAsync(session1, CancellationToken.None);
        await sut.AddSessionAsync(session2, CancellationToken.None);

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-30);
        var expired = await sut.GetExpiredSessionsAsync(cutoff, limit: 10, CancellationToken.None);

        expired.Should().ContainSingle();
        expired.Single().UserId.Should().Be(userId);
        expired.Single().DeviceId.Should().Be("d1");
    }

    [Fact]
    public async Task RemoveSession_AfterRemove_WipesHeartbeatZsetEntry()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-1);
        var session = Session(userId, "d1") with { LastHeartbeatAt = stale };
        await sut.AddSessionAsync(session, CancellationToken.None);

        await sut.RemoveSessionAsync(userId, "d1", CancellationToken.None);

        var expired = await sut.GetExpiredSessionsAsync(
            DateTimeOffset.UtcNow.AddSeconds(-30), limit: 10, CancellationToken.None);
        expired.Should().BeEmpty();
    }
}
