using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class ConversationRepositoryTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private ConversationRepository _repo = null!;

    public ConversationRepositoryTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    public Task InitializeAsync()
    {
        _context = _mongo.CreateContext();
        _repo = new ConversationRepository(_context);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TryCreateAsync_FirstWriter_RoundTripsConversation()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var conversation = Conversation.OpenDirect(userA, userB, now);

        var created = await _repo.TryCreateAsync(conversation, default);

        created.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(conversation.Id, default);
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(conversation.Id);
        loaded.Participants.Should().BeEquivalentTo(conversation.Participants);
        loaded.LastMessageAtUtc.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        loaded.LastMessagePreview.Should().BeNull();
    }

    [Fact]
    public async Task TryCreateAsync_SecondWriter_ReturnsFalse_AndKeepsOriginal()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var first = Conversation.OpenDirect(userA, userB, DateTimeOffset.UtcNow);
        await _repo.TryCreateAsync(first, default);

        // Same conversation Id (deterministic), different timestamp.
        var racyDuplicate = Conversation.OpenDirect(userA, userB, DateTimeOffset.UtcNow.AddSeconds(1));
        var second = await _repo.TryCreateAsync(racyDuplicate, default);

        second.Should().BeFalse();
        var loaded = await _repo.GetByIdAsync(first.Id, default);
        loaded!.CreatedAtUtc.Should().BeCloseTo(first.CreatedAtUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateLastMessageAsync_PersistsPreview()
    {
        var conversation = Conversation.OpenDirect(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        await _repo.TryCreateAsync(conversation, default);

        var sentAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var preview = new MessagePreview(conversation.Participants[0], "ping", sentAt, HasAttachments: false);
        await _repo.UpdateLastMessageAsync(conversation.Id, preview, sentAt, default);

        var loaded = await _repo.GetByIdAsync(conversation.Id, default);
        loaded!.LastMessagePreview.Should().NotBeNull();
        loaded.LastMessagePreview!.Body.Should().Be("ping");
        loaded.LastMessageAtUtc.Should().BeCloseTo(sentAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ListByParticipantAsync_ReturnsOnlyConversationsContainingUser_OrderedByLastMessageDesc()
    {
        var user = Guid.NewGuid();
        var c1 = Conversation.OpenDirect(user, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        var c2 = Conversation.OpenDirect(user, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-1));
        var noise = Conversation.OpenDirect(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        await _repo.TryCreateAsync(c1, default);
        await _repo.TryCreateAsync(c2, default);
        await _repo.TryCreateAsync(noise, default);

        var results = await _repo.ListByParticipantAsync(user, cursor: null, limit: 50, default);

        results.Should().HaveCount(2);
        results.Select(c => c.Id).Should().ContainInOrder(c2.Id, c1.Id);
    }

    [Fact]
    public async Task ListByParticipantAsync_WithCursor_PagesOlder()
    {
        var user = Guid.NewGuid();
        var older = Conversation.OpenDirect(user, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        var newer = Conversation.OpenDirect(user, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-1));
        await _repo.TryCreateAsync(older, default);
        await _repo.TryCreateAsync(newer, default);

        var page = await _repo.ListByParticipantAsync(
            user,
            cursor: new ConversationCursor(newer.LastMessageAtUtc, newer.Id),
            limit: 50,
            default);

        page.Should().ContainSingle().Which.Id.Should().Be(older.Id);
    }
}
