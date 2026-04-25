using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;

namespace PresenceService.IntegrationTests.Persistence;

[Collection(IntegrationCollection.Name)]
public class LastSeenRepositoryTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public LastSeenRepositoryTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveChangesAsync_NewLastSeen_PersistsRowAndDispatchesOfflineEvent()
    {
        var userId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
            var ls = LastSeen.Create(userId, Platform.Mobile, ts.AddMinutes(-1));
            ls.Update(Platform.Mobile, ts);
            repo.Upsert(ls);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
            var loaded = await repo.GetAsync(userId, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded!.LastPlatform.Should().Be(Platform.Mobile);
        }

        var publishedOffline = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<UserWentOfflineEvent>()
            .Where(e => e.UserId == userId)
            .ToList();
        publishedOffline.Should().HaveCount(1);
        publishedOffline[0].EventType.Should().Be("presence.user.offline.v1");
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatedLastSeen_DispatchesOfflineEvent()
    {
        var userId = Guid.NewGuid();
        var initialTs = DateTimeOffset.UtcNow.AddMinutes(-10);

        // seed: persist initial row WITHOUT triggering events
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<PresenceDbContext>();
            ctx.LastSeens.Add(LastSeen.Create(userId, Platform.Web, initialTs));
            await ctx.SaveChangesAsync();
        }
        _factory.OutboxWriter.Clear();

        // act: load, update, save
        var newTs = DateTimeOffset.UtcNow;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
            var loaded = await repo.GetAsync(userId, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded!.Update(Platform.Desktop, newTs);
            repo.Upsert(loaded);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        var publishedOffline = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<UserWentOfflineEvent>()
            .Where(e => e.UserId == userId)
            .ToList();
        publishedOffline.Should().HaveCount(1);
        publishedOffline[0].LastSeenAt.Should().BeCloseTo(newTs, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SaveChangesAsync_NoChanges_PublishesNoEvents()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
        await repo.SaveChangesAsync(CancellationToken.None);

        _factory.OutboxWriter.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchedEvents_AreScopedToPresenceTopic()
    {
        var userId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;

        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
        var ls = LastSeen.Create(userId, Platform.Web, ts);
        ls.Update(Platform.Web, ts);
        repo.Upsert(ls);
        await repo.SaveChangesAsync(CancellationToken.None);

        var topics = _factory.OutboxWriter.Published.Select(p => p.Topic).Distinct().ToList();
        topics.Should().ContainSingle().Which.Should().Be(KafkaTopicNames.PresenceEvents);
    }
}
