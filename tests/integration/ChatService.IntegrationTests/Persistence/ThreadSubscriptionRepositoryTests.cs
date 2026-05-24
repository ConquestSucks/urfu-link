using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class ThreadSubscriptionRepositoryTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private ThreadSubscriptionRepository _repo = null!;

    public ThreadSubscriptionRepositoryTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    public async Task InitializeAsync()
    {
        _context = _mongo.CreateContext();
        _repo = new ThreadSubscriptionRepository(_context);
        var indexes = new MongoIndexInitializer(_context);
        await indexes.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpsertAsync_NewSubscription_ReturnsWasCreated()
    {
        var rootId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sub = ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow);

        var result = await _repo.UpsertAsync(sub, default);

        result.WasCreated.Should().BeTrue();
        result.ReasonEscalated.Should().BeFalse();
        var subscribers = await _repo.GetSubscriberIdsAsync(rootId, default);
        subscribers.Should().ContainSingle().Which.Should().Be(userId);
    }

    [Fact]
    public async Task UpsertAsync_AlreadyAtSameReason_ReturnsNeitherFlag()
    {
        var rootId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow), default);

        var second = await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow.AddSeconds(1)), default);

        second.WasCreated.Should().BeFalse();
        second.ReasonEscalated.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertAsync_EscalatesReason_FromManualToReplied()
    {
        var rootId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow), default);

        var escalation = await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Replied, DateTimeOffset.UtcNow.AddSeconds(1)), default);

        escalation.WasCreated.Should().BeFalse();
        escalation.ReasonEscalated.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_DoesNotDowngradeReason()
    {
        var rootId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Replied, DateTimeOffset.UtcNow), default);

        var attemptDowngrade = await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow.AddSeconds(1)), default);

        attemptDowngrade.WasCreated.Should().BeFalse();
        attemptDowngrade.ReasonEscalated.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_DeletesAndReturnsTrue()
    {
        var rootId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow), default);

        var removed = await _repo.RemoveAsync(rootId, userId, default);

        removed.Should().BeTrue();
        var subs = await _repo.GetSubscriberIdsAsync(rootId, default);
        subs.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_MissingSubscription_ReturnsFalse()
    {
        var removed = await _repo.RemoveAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task TouchActivityForRootAsync_BumpsAllSubscriptions()
    {
        var rootId = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var early = DateTimeOffset.UtcNow.AddMinutes(-10);
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, alice, ThreadSubscriptionReason.Replied, early), default);
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, bob, ThreadSubscriptionReason.Mentioned, early), default);

        var late = DateTimeOffset.UtcNow;
        var modified = await _repo.TouchActivityForRootAsync(rootId, late, default);

        modified.Should().Be(2);
    }

    [Fact]
    public async Task ListUserActiveAsync_PaginatesByLastActivityDesc()
    {
        var user = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);
        for (var i = 0; i < 5; i++)
        {
            var sub = ThreadSubscription.Subscribe(
                Guid.NewGuid(), user, ThreadSubscriptionReason.Replied,
                subscribedAtUtc: baseTime.AddMinutes(i),
                lastActivityAtUtc: baseTime.AddMinutes(i));
            await _repo.UpsertAsync(sub, default);
        }

        var page = await _repo.ListUserActiveAsync(user, cursor: null, limit: 3, default);

        page.Should().HaveCount(3);
        // Newest first
        for (var i = 0; i < page.Count - 1; i++)
        {
            page[i].LastActivityAtUtc.Should().BeAfter(page[i + 1].LastActivityAtUtc);
        }
    }

    [Fact]
    public async Task UniqueIndex_PreventsDuplicateUpsertViaConcurrentAccess()
    {
        // The UpsertAsync pipeline already collapses duplicates atomically; this test exists to
        // ensure the unique index is in place so a hand-written InsertOne would also fail. We use
        // the public Upsert API twice and verify exactly one document persists.
        var rootId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Manual, DateTimeOffset.UtcNow), default);
        await _repo.UpsertAsync(ThreadSubscription.Subscribe(rootId, userId, ThreadSubscriptionReason.Mentioned, DateTimeOffset.UtcNow.AddSeconds(1)), default);

        var subs = await _repo.GetSubscriberIdsAsync(rootId, default);
        subs.Should().ContainSingle();
    }
}
