using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;
using Urfu.Link.Services.Presence.Infrastructure;
using Urfu.Link.Services.Presence.Workers;

namespace PresenceService.IntegrationTests.Workers;

[Collection(IntegrationCollection.Name)]
public class PresenceSweeperWorkerTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public PresenceSweeperWorkerTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SweepOnceAsync(CancellationToken ct = default)
    {
        var sp = _factory.Services;
        using var worker = new PresenceSweeperWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IPresenceSessionStore>(),
            sp.GetRequiredService<IOptions<PresenceOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            NullLogger<PresenceSweeperWorker>.Instance);
        await worker.SweepAsync(ct);
    }

    [Fact]
    public async Task Sweep_NoExpired_DoesNothing()
    {
        await SweepOnceAsync();

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_RemovesExpiredSession()
    {
        var sessions = _factory.Services.GetRequiredService<IPresenceSessionStore>();
        var userId = Guid.NewGuid();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-5);
        await sessions.AddSessionAsync(new PresenceSession(
            userId, "d1", Platform.Web, PresenceStatus.Online, null, stale, stale),
            CancellationToken.None);

        await SweepOnceAsync();

        var remaining = await sessions.GetSessionsAsync(userId, CancellationToken.None);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_OnLastExpiredSession_PublishesOfflineAndPersistsLastSeen()
    {
        var sessions = _factory.Services.GetRequiredService<IPresenceSessionStore>();
        var userId = Guid.NewGuid();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-5);
        await sessions.AddSessionAsync(new PresenceSession(
            userId, "d1", Platform.Mobile, PresenceStatus.Online, null, stale, stale),
            CancellationToken.None);

        await SweepOnceAsync();

        var offline = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .SingleOrDefault(e => e.UserId == userId);
        offline.Should().NotBeNull();

        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();
        var ls = await repo.GetAsync(userId, CancellationToken.None);
        ls.Should().NotBeNull();
        ls!.LastPlatform.Should().Be(Platform.Mobile);
    }

    [Fact]
    public async Task Sweep_OneOfTwoExpired_NoOfflineEventBecauseOtherStillAlive()
    {
        var sessions = _factory.Services.GetRequiredService<IPresenceSessionStore>();
        var userId = Guid.NewGuid();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-5);
        var fresh = DateTimeOffset.UtcNow;
        await sessions.AddSessionAsync(new PresenceSession(
            userId, "d1", Platform.Mobile, PresenceStatus.Online, null, stale, stale),
            CancellationToken.None);
        await sessions.AddSessionAsync(new PresenceSession(
            userId, "d2", Platform.Web, PresenceStatus.Online, null, fresh, fresh),
            CancellationToken.None);

        await SweepOnceAsync();

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Where(e => e.UserId == userId)
            .Should().BeEmpty();
        var remaining = await sessions.GetSessionsAsync(userId, CancellationToken.None);
        remaining.Should().HaveCount(1);
    }

    [Fact]
    public async Task Sweep_AlreadyRemovedSession_PublishesNoSecondaryEvent()
    {
        var sessions = _factory.Services.GetRequiredService<IPresenceSessionStore>();
        var userId = Guid.NewGuid();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-5);
        await sessions.AddSessionAsync(new PresenceSession(
            userId, "d1", Platform.Web, PresenceStatus.Online, null, stale, stale),
            CancellationToken.None);

        await SweepOnceAsync();
        var firstRound = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Count(e => e.UserId == userId);
        firstRound.Should().Be(1);

        // Sweep again — nothing expired anymore, no event should be published.
        await SweepOnceAsync();
        var secondRound = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<UserWentOfflineEvent>()
            .Count(e => e.UserId == userId);
        secondRound.Should().Be(1);
    }
}
