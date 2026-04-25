using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.IntegrationTests.Privacy;

[Collection(IntegrationCollection.Name)]
public class PrivacyProjectionTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public PrivacyProjectionTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TriggerPrivacyChanged_UpdatesRedisProjection()
    {
        var userId = Guid.NewGuid();

        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, userId, showOnlineStatus: false, showLastVisitTime: true);

        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivacyProjectionStore>();
        var loaded = await store.GetAsync(userId, CancellationToken.None);
        loaded.Should().Be(new PrivacySettings(false, true));
    }

    [Fact]
    public async Task TriggerPrivacyChanged_OverridesPreviousProjection()
    {
        var userId = Guid.NewGuid();
        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, userId, false, false);

        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, userId, true, true);

        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivacyProjectionStore>();
        (await store.GetAsync(userId, CancellationToken.None))
            .Should().Be(PrivacySettings.Default);
    }

    [Fact]
    public async Task PrivacyChangedFromKafka_ImmediatelyReflectsInRestResponse()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<Urfu.Link.Services.Presence.Domain.Interfaces.IPresenceSessionStore>();
            await sessions.AddSessionAsync(new Urfu.Link.Services.Presence.Domain.ValueObjects.PresenceSession(
                targetId, "d1",
                Urfu.Link.Services.Presence.Domain.Enums.Platform.Web,
                Urfu.Link.Services.Presence.Domain.Enums.PresenceStatus.Online,
                CustomActivity: null,
                ConnectedAt: DateTimeOffset.UtcNow,
                LastHeartbeatAt: DateTimeOffset.UtcNow), CancellationToken.None);
        }

        // Before: privacy is default (everything visible) → REST returns Online.
        var before = await _factory.CreateClient().GetFromJsonAsync<Urfu.Link.Services.Presence.Application.Contracts.Responses.PresenceInfoResponse>(
            $"/api/v1/presence/users/{targetId}");
        before!.Status.Should().Be(Urfu.Link.Services.Presence.Domain.Enums.PresenceStatus.Online);

        // Kafka projects privacy=hide-online.
        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, targetId, false, true);

        // REST now reflects offline (status hidden).
        var after = await _factory.CreateClient().GetFromJsonAsync<Urfu.Link.Services.Presence.Application.Contracts.Responses.PresenceInfoResponse>(
            $"/api/v1/presence/users/{targetId}");
        after!.Status.Should().Be(Urfu.Link.Services.Presence.Domain.Enums.PresenceStatus.Offline);
        after.Platforms.Should().BeEmpty();
    }

    [Fact]
    public async Task UnsubscribedEventType_IsIgnored()
    {
        var userId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<Urfu.Link.Services.Presence.Messaging.IKafkaMessageHandler>().ToList();
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(new { UserId = userId });
        foreach (var h in handlers)
        {
            await h.HandleAsync("user.something.unrelated.v1", payload, CancellationToken.None);
        }

        var store = scope.ServiceProvider.GetRequiredService<IPrivacyProjectionStore>();
        (await store.GetAsync(userId, CancellationToken.None))
            .Should().Be(PrivacySettings.Default);
    }
}
