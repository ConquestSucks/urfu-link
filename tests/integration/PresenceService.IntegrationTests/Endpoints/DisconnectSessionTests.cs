using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class DisconnectSessionTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public DisconnectSessionTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Post_RemovesCurrentUserSessionAndBroadcastsOffline()
    {
        var targetId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();
        const string deviceId = "web-tab-close";
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            await sessions.AddSessionAsync(new PresenceSession(
                targetId,
                deviceId,
                Platform.Web,
                PresenceStatus.Online,
                CustomActivity: null,
                ConnectedAt: now,
                LastHeartbeatAt: now), CancellationToken.None);
        }

        var received = new TaskCompletionSource<(Guid, PresenceStatus, Platform[], DateTimeOffset?)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscriber = await TestPresenceHubClient.ConnectAsync(_factory, subscriberId);
        subscriber.On<Guid, PresenceStatus, Platform[], DateTimeOffset?>(
            "UserPresenceChanged",
            (userId, status, platforms, lastSeenAt) =>
            {
                if (userId == targetId && status == PresenceStatus.Offline)
                {
                    received.TrySetResult((userId, status, platforms, lastSeenAt));
                }
            });
        await subscriber.InvokeAsync("SubscribeToUsers", new[] { targetId });

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(targetId);
        var response = await _factory.CreateClient()
            .PostAsync($"/api/v1/presence/sessions/{deviceId}/disconnect", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var observed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observed.Item1.Should().Be(targetId);
        observed.Item2.Should().Be(PresenceStatus.Offline);
        observed.Item3.Should().BeEmpty();
        observed.Item4.Should().NotBeNull();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        var remaining = await store.GetSessionsAsync(targetId, CancellationToken.None);
        remaining.Should().BeEmpty();

        var lastSeen = verifyScope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
        var persisted = await lastSeen.GetAsync(targetId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.LastPlatform.Should().Be(Platform.Web);
    }

    [Fact]
    public async Task Post_DoesNotRemoveAnotherUsersSessionWithSameDeviceId()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        const string deviceId = "shared-device-id";
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            await sessions.AddSessionAsync(new PresenceSession(
                otherUserId,
                deviceId,
                Platform.Web,
                PresenceStatus.Online,
                CustomActivity: null,
                ConnectedAt: now,
                LastHeartbeatAt: now), CancellationToken.None);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(currentUserId);
        var response = await _factory.CreateClient()
            .PostAsync($"/api/v1/presence/sessions/{deviceId}/disconnect", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        var otherSessions = await store.GetSessionsAsync(otherUserId, CancellationToken.None);
        otherSessions.Should().ContainSingle(s => s.DeviceId == deviceId);
    }

    [Fact]
    public async Task Post_MissingSessionStillBroadcastsCurrentOfflineState()
    {
        var targetId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await using var subscriber = await TestPresenceHubClient.ConnectAsync(_factory, subscriberId);
        await subscriber.InvokeAsync("SubscribeToUsers", new[] { targetId });

        var received = new TaskCompletionSource<(Guid, PresenceStatus, Platform[], DateTimeOffset?)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.On<Guid, PresenceStatus, Platform[], DateTimeOffset?>(
            "UserPresenceChanged",
            (userId, status, platforms, lastSeenAt) =>
            {
                if (userId == targetId && status == PresenceStatus.Offline)
                {
                    received.TrySetResult((userId, status, platforms, lastSeenAt));
                }
            });

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(targetId);
        var response = await _factory.CreateClient()
            .PostAsync("/api/v1/presence/sessions/already-removed/disconnect", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var observed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observed.Item1.Should().Be(targetId);
        observed.Item2.Should().Be(PresenceStatus.Offline);
        observed.Item3.Should().BeEmpty();
    }
}
