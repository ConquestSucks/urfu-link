using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;

namespace PresenceService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class PresenceHubLifecycleTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public PresenceHubLifecycleTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Connect_FirstSession_PublishesUserCameOnline()
    {
        var userId = Guid.NewGuid();
        await using var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web);

        await WaitForAsync(() => _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserCameOnlineEvent>().Any(e => e.UserId == userId));

        var ev = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserCameOnlineEvent>()
            .Single(e => e.UserId == userId);
        ev.Platforms.Should().Equal(Platform.Web);
    }

    [Fact]
    public async Task Connect_FirstSession_BroadcastsToSelfGroup()
    {
        var userId = Guid.NewGuid();
        var received = new TaskCompletionSource<(Guid, PresenceStatus, Platform[], DateTimeOffset?)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Mobile);
        connection.On<Guid, PresenceStatus, Platform[], DateTimeOffset?>(
            "UserPresenceChanged", (uid, status, platforms, lastSeen) =>
            {
                if (uid == userId) received.TrySetResult((uid, status, platforms, lastSeen));
            });

        // Trigger another broadcast by calling SetStatus, since the very first
        // broadcast happens before the client's `On` handler is registered.
        await connection.InvokeAsync("SetStatus", PresenceStatus.Online, null);

        var (uid2, status2, platforms2, _) = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        uid2.Should().Be(userId);
        status2.Should().Be(PresenceStatus.Online);
        platforms2.Should().Equal(Platform.Mobile);
    }

    [Fact]
    public async Task Disconnect_LastSession_PublishesOfflineAndPersistsLastSeen()
    {
        var userId = Guid.NewGuid();
        var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Desktop);

        // Wait until the connect-time UserCameOnlineEvent has been enqueued, then
        // clear so we can isolate the disconnect-time UserWentOfflineEvent.
        await WaitForAsync(() => _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserCameOnlineEvent>().Any(e => e.UserId == userId));
        _factory.OutboxWriter.Clear();

        await connection.StopAsync();
        await connection.DisposeAsync();

        await WaitForAsync(() => _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>().Any(e => e.UserId == userId),
            timeout: TimeSpan.FromSeconds(40));

        var offlineEv = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Single(e => e.UserId == userId);
        offlineEv.LastSeenAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60));

        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
        var ls = await repo.GetAsync(userId, CancellationToken.None);
        ls.Should().NotBeNull();
        ls!.LastPlatform.Should().Be(Platform.Desktop);
    }

    [Fact]
    public async Task Disconnect_LastSession_RemovesSessionFromRedis()
    {
        var userId = Guid.NewGuid();
        var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId);

        await connection.StopAsync();
        await connection.DisposeAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        await WaitForAsync(async () =>
        {
            var s = await sessions.GetSessionsAsync(userId, CancellationToken.None);
            return s.Count == 0;
        }, timeout: TimeSpan.FromSeconds(40));
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(50);
        }
        predicate().Should().BeTrue("expected condition to be true within timeout");
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate()) return;
            await Task.Delay(50);
        }
        (await predicate()).Should().BeTrue("expected condition to be true within timeout");
    }
}
