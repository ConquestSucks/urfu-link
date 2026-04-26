using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MessageRepositoryReactionsTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private MessageRepository _repo = null!;
    private MongoIndexInitializer _indexes = null!;

    private const string ConvId = "conv-reactions";

    public MessageRepositoryReactionsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    public async Task InitializeAsync()
    {
        _context = _mongo.CreateContext();
        _repo = new MessageRepository(_context);
        _indexes = new MongoIndexInitializer(_context);
        await _indexes.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Message> InsertNewAsync()
    {
        var msg = Message.Send(
            id: Guid.NewGuid(),
            conversationId: ConvId,
            senderId: Guid.NewGuid(),
            body: "hi",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: $"client-{Guid.NewGuid():N}",
            createdAtUtc: DateTimeOffset.UtcNow);
        await _repo.InsertAsync(msg, default);
        return msg;
    }

    [Fact]
    public async Task AddReactionAsync_NewReaction_PersistsAndReturnsTrue()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();

        var added = await _repo.AddReactionAsync(msg.Id, new Reaction(user, "👍", DateTimeOffset.UtcNow), default);

        added.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().ContainSingle(r => r.UserId == user && r.Emoji == "👍");
    }

    [Fact]
    public async Task AddReactionAsync_SameUserSameEmoji_IsIdempotent()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();
        await _repo.AddReactionAsync(msg.Id, new Reaction(user, "👍", DateTimeOffset.UtcNow), default);

        var second = await _repo.AddReactionAsync(msg.Id, new Reaction(user, "👍", DateTimeOffset.UtcNow), default);

        second.Should().BeFalse();
        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddReactionAsync_SameUserDifferentEmoji_ReplacesPriorReaction()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();
        await _repo.AddReactionAsync(msg.Id, new Reaction(user, "👍", DateTimeOffset.UtcNow), default);

        var changed = await _repo.AddReactionAsync(msg.Id, new Reaction(user, "❤", DateTimeOffset.UtcNow.AddSeconds(1)), default);

        changed.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().ContainSingle();
        loaded.Reactions[0].Emoji.Should().Be("❤");
    }

    [Fact]
    public async Task AddReactionAsync_DifferentUsers_AccumulateIndependently()
    {
        var msg = await InsertNewAsync();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _repo.AddReactionAsync(msg.Id, new Reaction(userA, "👍", DateTimeOffset.UtcNow), default);
        await _repo.AddReactionAsync(msg.Id, new Reaction(userB, "👍", DateTimeOffset.UtcNow), default);

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task RemoveReactionAsync_ExistingReaction_RemovesAndReturnsTrue()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();
        await _repo.AddReactionAsync(msg.Id, new Reaction(user, "👍", DateTimeOffset.UtcNow), default);

        var removed = await _repo.RemoveReactionAsync(msg.Id, user, "👍", default);

        removed.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveReactionAsync_DifferentEmoji_ReturnsFalse()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();
        await _repo.AddReactionAsync(msg.Id, new Reaction(user, "👍", DateTimeOffset.UtcNow), default);

        var removed = await _repo.RemoveReactionAsync(msg.Id, user, "❤", default);

        removed.Should().BeFalse();
        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddReactionAsync_ConcurrentSameUserDifferentEmojis_LeavesExactlyOneReaction()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();
        var emojis = new[] { "👍", "❤", "🔥", "🎉", "😂", "👀", "🚀", "💯", "✨", "🙌" };

        // Fire all 10 add reactions concurrently. Without atomic pull-then-push the database
        // can end up with multiple reactions from the same user; with the pipeline update we
        // are guaranteed a single reaction.
        var tasks = emojis.Select(e =>
            _repo.AddReactionAsync(msg.Id, new Reaction(user, e, DateTimeOffset.UtcNow), default)).ToArray();
        await Task.WhenAll(tasks);

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().ContainSingle(r => r.UserId == user);
        emojis.Should().Contain(loaded.Reactions[0].Emoji);
    }
}
