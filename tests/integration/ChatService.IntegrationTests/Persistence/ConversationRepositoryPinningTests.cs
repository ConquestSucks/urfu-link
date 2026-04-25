using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class ConversationRepositoryPinningTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private ConversationRepository _repo = null!;

    public ConversationRepositoryPinningTests(MongoFixture mongo)
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

    private async Task<Conversation> CreateNewAsync()
    {
        var conv = Conversation.OpenDirect(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        await _repo.TryCreateAsync(conv, default);
        return conv;
    }

    [Fact]
    public async Task AddPinnedMessageAsync_FirstTime_AddsAndReturnsTrue()
    {
        var conv = await CreateNewAsync();
        var msgId = Guid.NewGuid();

        var pinned = await _repo.AddPinnedMessageAsync(conv.Id, msgId, maxPinned: 5, default);

        pinned.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(conv.Id, default);
        loaded!.PinnedMessageIds.Should().ContainSingle(id => id == msgId);
    }

    [Fact]
    public async Task AddPinnedMessageAsync_AlreadyPinned_ReturnsFalse()
    {
        var conv = await CreateNewAsync();
        var msgId = Guid.NewGuid();
        await _repo.AddPinnedMessageAsync(conv.Id, msgId, 5, default);

        var second = await _repo.AddPinnedMessageAsync(conv.Id, msgId, 5, default);

        second.Should().BeFalse();
        var loaded = await _repo.GetByIdAsync(conv.Id, default);
        loaded!.PinnedMessageIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddPinnedMessageAsync_AtCap_ReturnsFalse()
    {
        var conv = await CreateNewAsync();
        for (var i = 0; i < 5; i++)
        {
            (await _repo.AddPinnedMessageAsync(conv.Id, Guid.NewGuid(), 5, default)).Should().BeTrue();
        }

        var sixth = await _repo.AddPinnedMessageAsync(conv.Id, Guid.NewGuid(), 5, default);

        sixth.Should().BeFalse();
        var loaded = await _repo.GetByIdAsync(conv.Id, default);
        loaded!.PinnedMessageIds.Should().HaveCount(5);
    }

    [Fact]
    public async Task AddPinnedMessageAsync_MissingConversation_ReturnsFalse()
    {
        var pinned = await _repo.AddPinnedMessageAsync("does-not-exist", Guid.NewGuid(), 5, default);

        pinned.Should().BeFalse();
    }

    [Fact]
    public async Task RemovePinnedMessageAsync_PinnedMessage_RemovesAndReturnsTrue()
    {
        var conv = await CreateNewAsync();
        var msgId = Guid.NewGuid();
        await _repo.AddPinnedMessageAsync(conv.Id, msgId, 5, default);

        var unpinned = await _repo.RemovePinnedMessageAsync(conv.Id, msgId, default);

        unpinned.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(conv.Id, default);
        loaded!.PinnedMessageIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovePinnedMessageAsync_NotPinned_ReturnsFalse()
    {
        var conv = await CreateNewAsync();

        var unpinned = await _repo.RemovePinnedMessageAsync(conv.Id, Guid.NewGuid(), default);

        unpinned.Should().BeFalse();
    }
}
