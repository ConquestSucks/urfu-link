using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MessageRepositoryEditDeleteTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private MessageRepository _repo = null!;
    private MongoIndexInitializer _indexes = null!;

    private const string ConvId = "conv-edit-delete";

    public MessageRepositoryEditDeleteTests(MongoFixture mongo)
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
            body: "hello",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: $"client-{Guid.NewGuid():N}",
            createdAtUtc: DateTimeOffset.UtcNow);
        await _repo.InsertAsync(msg, default);
        return msg;
    }

    [Fact]
    public async Task ApplyEditAsync_NotDeleted_UpdatesBodyAndHistory_AndReturnsTrue()
    {
        var msg = await InsertNewAsync();
        var historyEntry = new EditHistoryEntry("hello", msg.CreatedAtUtc);
        var editedAt = msg.CreatedAtUtc.AddMinutes(1);

        var changed = await _repo.ApplyEditAsync(
            msg.Id,
            "edited",
            new[] { Guid.NewGuid() },
            historyEntry,
            editedAt,
            default);

        changed.Should().BeTrue();

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.Body.Should().Be("edited");
        loaded.EditedAtUtc.Should().BeCloseTo(editedAt, TimeSpan.FromSeconds(1));
        loaded.EditHistory.Should().HaveCount(1);
        loaded.EditHistory[0].Body.Should().Be("hello");
        loaded.Mentions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApplyEditAsync_OnDeletedMessage_ReturnsFalse()
    {
        var msg = await InsertNewAsync();
        await _repo.ApplyDeleteForEveryoneAsync(msg.Id, msg.SenderId, DateTimeOffset.UtcNow, default);

        var changed = await _repo.ApplyEditAsync(
            msg.Id,
            "after-delete",
            Array.Empty<Guid>(),
            new EditHistoryEntry("hello", msg.CreatedAtUtc),
            DateTimeOffset.UtcNow,
            default);

        changed.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyEditAsync_AppendsHistoryAcrossMultipleEdits()
    {
        var msg = await InsertNewAsync();

        await _repo.ApplyEditAsync(
            msg.Id, "first", Array.Empty<Guid>(),
            new EditHistoryEntry("hello", msg.CreatedAtUtc),
            msg.CreatedAtUtc.AddMinutes(1), default);
        await _repo.ApplyEditAsync(
            msg.Id, "second", Array.Empty<Guid>(),
            new EditHistoryEntry("first", msg.CreatedAtUtc.AddMinutes(1)),
            msg.CreatedAtUtc.AddMinutes(2), default);

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.EditHistory.Should().HaveCount(2);
        loaded.EditHistory[0].Body.Should().Be("hello");
        loaded.EditHistory[1].Body.Should().Be("first");
        loaded.Body.Should().Be("second");
    }

    [Fact]
    public async Task ApplyDeleteForEveryoneAsync_TombstonesMessage_AndReturnsTrue()
    {
        var msg = await InsertNewAsync();
        await _repo.AddReactionAsync(msg.Id, new Reaction(Guid.NewGuid(), "👍", DateTimeOffset.UtcNow), default);
        var deletedAt = DateTimeOffset.UtcNow;

        var deleted = await _repo.ApplyDeleteForEveryoneAsync(msg.Id, msg.SenderId, deletedAt, default);

        deleted.Should().BeTrue();

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.State.Should().Be(MessageState.Deleted);
        loaded.DeleteMode.Should().Be(DeleteMode.ForEveryone);
        loaded.DeletedBy.Should().Be(msg.SenderId);
        loaded.DeletedAtUtc.Should().BeCloseTo(deletedAt, TimeSpan.FromSeconds(1));
        loaded.Body.Should().BeEmpty();
        loaded.Reactions.Should().BeEmpty();
        loaded.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyDeleteForEveryoneAsync_AlreadyDeleted_ReturnsFalse()
    {
        var msg = await InsertNewAsync();
        await _repo.ApplyDeleteForEveryoneAsync(msg.Id, msg.SenderId, DateTimeOffset.UtcNow, default);

        var second = await _repo.ApplyDeleteForEveryoneAsync(msg.Id, msg.SenderId, DateTimeOffset.UtcNow.AddSeconds(5), default);

        second.Should().BeFalse();
    }

    [Fact]
    public async Task AddHiddenForAsync_NewUser_ReturnsTrueAndAppendsToHiddenFor()
    {
        var msg = await InsertNewAsync();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        (await _repo.AddHiddenForAsync(msg.Id, userA, default)).Should().BeTrue();
        (await _repo.AddHiddenForAsync(msg.Id, userB, default)).Should().BeTrue();

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.HiddenFor.Should().BeEquivalentTo(new[] { userA, userB });
    }

    [Fact]
    public async Task AddHiddenForAsync_SameUserTwice_IsIdempotent()
    {
        var msg = await InsertNewAsync();
        var user = Guid.NewGuid();
        await _repo.AddHiddenForAsync(msg.Id, user, default);

        var second = await _repo.AddHiddenForAsync(msg.Id, user, default);

        second.Should().BeFalse();
        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded!.HiddenFor.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdsAsync_FiltersByConversationId()
    {
        var conv1 = $"conv-1-{Guid.NewGuid():N}";
        var conv2 = $"conv-2-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();
        var m1 = Message.Send(Guid.NewGuid(), conv1, sender, "in conv1", Array.Empty<Attachment>(), $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var m2 = Message.Send(Guid.NewGuid(), conv2, sender, "in conv2", Array.Empty<Attachment>(), $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        await _repo.InsertAsync(m1, default);
        await _repo.InsertAsync(m2, default);

        var fromConv1 = await _repo.GetByIdsAsync(conv1, new[] { m1.Id, m2.Id }, default);

        fromConv1.Should().HaveCount(1);
        fromConv1[0].Id.Should().Be(m1.Id);
    }
}
