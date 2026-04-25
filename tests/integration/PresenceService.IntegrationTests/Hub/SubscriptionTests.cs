using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;

namespace PresenceService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class SubscriptionTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public SubscriptionTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SubscribeToUsers_ReceivesPresenceChangesForOtherUser()
    {
        var subscriberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        await using var subscriber = await TestPresenceHubClient.ConnectAsync(_factory, subscriberId, Platform.Web);
        var received = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.On<Guid, PresenceStatus, Platform[], DateTimeOffset?>("UserPresenceChanged",
            (uid, _, _, _) =>
            {
                if (uid == targetId) received.TrySetResult(uid);
            });

        await subscriber.InvokeAsync("SubscribeToUsers", new[] { targetId });

        // Now connect target — its OnConnectedAsync broadcasts to presence:{targetId}
        // group, where subscriber is now a member.
        await using var target = await TestPresenceHubClient.ConnectAsync(_factory, targetId, Platform.Mobile);

        var observed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observed.Should().Be(targetId);
    }

    [Fact]
    public async Task UnsubscribeFromUsers_StopsReceivingChanges()
    {
        var subscriberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        await using var subscriber = await TestPresenceHubClient.ConnectAsync(_factory, subscriberId);
        await subscriber.InvokeAsync("SubscribeToUsers", new[] { targetId });
        await subscriber.InvokeAsync("UnsubscribeFromUsers", new[] { targetId });

        var received = false;
        subscriber.On<Guid, PresenceStatus, Platform[], DateTimeOffset?>("UserPresenceChanged",
            (uid, _, _, _) =>
            {
                if (uid == targetId) received = true;
            });

        await using var target = await TestPresenceHubClient.ConnectAsync(_factory, targetId);
        await Task.Delay(500);

        received.Should().BeFalse();
    }
}
