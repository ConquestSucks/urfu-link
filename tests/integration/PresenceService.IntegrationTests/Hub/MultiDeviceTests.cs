using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace PresenceService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class MultiDeviceTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public MultiDeviceTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TwoDevices_Connect_PublishesSingleOnlineEvent()
    {
        var userId = Guid.NewGuid();
        await using var c1 = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web, "d1");
        await using var c2 = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Mobile, "d2");

        await Wait(() => _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserCameOnlineEvent>().Any(e => e.UserId == userId));

        var onlineEvents = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserCameOnlineEvent>()
            .Where(e => e.UserId == userId)
            .ToList();
        onlineEvents.Should().HaveCount(1, "second device must not re-publish UserCameOnline");
    }

    [Fact]
    public async Task TwoDevices_OneDisconnects_StaysOnlineNoOfflineEvent()
    {
        var userId = Guid.NewGuid();
        await using var c1 = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web, "d1");
        var c2 = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Mobile, "d2");

        await Wait(() => _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserCameOnlineEvent>().Any(e => e.UserId == userId));
        _factory.OutboxWriter.Clear();

        await c2.StopAsync();
        await c2.DisposeAsync();

        // Give the server time to process disconnect.
        await Task.Delay(500);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Where(e => e.UserId == userId)
            .Should().BeEmpty();

        await using var scope = _factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        var remaining = await sessions.GetSessionsAsync(userId, CancellationToken.None);
        remaining.Should().HaveCount(1).And.OnlyContain(s => s.DeviceId == "d1");
    }

    [Fact]
    public async Task TwoDevices_BothDisconnect_PublishesOfflineEventOnce()
    {
        var userId = Guid.NewGuid();
        var c1 = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web, "d1");
        var c2 = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Mobile, "d2");
        _factory.OutboxWriter.Clear();

        await c1.StopAsync();
        await c1.DisposeAsync();
        await c2.StopAsync();
        await c2.DisposeAsync();

        await Wait(() => _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>().Any(e => e.UserId == userId));

        var offlineEvents = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Where(e => e.UserId == userId)
            .ToList();
        offlineEvents.Should().HaveCount(1, "only the last disconnect triggers UserWentOffline");
    }

    [Fact]
    public async Task Heartbeat_RefreshesSessionScoreInZset()
    {
        var userId = Guid.NewGuid();
        await using var conn = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web, "d1");

        await using var scope = _factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        var before = (await sessions.GetSessionsAsync(userId, CancellationToken.None))
            .Single().LastHeartbeatAt;

        await Task.Delay(100);
        await conn.InvokeAsync("Heartbeat");

        var after = (await sessions.GetSessionsAsync(userId, CancellationToken.None))
            .Single().LastHeartbeatAt;
        after.Should().BeAfter(before);
    }

    [Fact]
    public async Task SetStatus_DoNotDisturb_AggregateBecomesDoNotDisturb()
    {
        var userId = Guid.NewGuid();
        await using var conn = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web, "d1");

        await conn.InvokeAsync("SetStatus", PresenceStatus.DoNotDisturb, "Heads down");

        await using var scope = _factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        var session = (await sessions.GetSessionsAsync(userId, CancellationToken.None)).Single();
        session.Status.Should().Be(PresenceStatus.DoNotDisturb);
        session.CustomActivity.Should().Be("Heads down");
    }

    private static async Task Wait(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(50);
        }
        predicate().Should().BeTrue("timeout");
    }
}
