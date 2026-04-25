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
