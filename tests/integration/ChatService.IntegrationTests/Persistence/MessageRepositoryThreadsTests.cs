using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MessageRepositoryThreadsTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private MessageRepository _repo = null!;

    private const string ConvId = "conv-thread-tests";

    public MessageRepositoryThreadsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    public async Task InitializeAsync()
    {
        _context = _mongo.CreateContext();
        _repo = new MessageRepository(_context);
        var indexes = new MongoIndexInitializer(_context);
        await indexes.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InsertAsync_ThreadReply_RoundTripsThreadRootId()
    {
        var sender = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var reply = Message.SendAsThreadReply(
            id: Guid.NewGuid(),
            conversationId: ConvId,
            senderId: sender,
            body: "reply",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: $"c-{Guid.NewGuid():N}",
            createdAtUtc: DateTimeOffset.UtcNow,
            threadRootId: rootId);

        await _repo.InsertAsync(reply, default);

        var loaded = await _repo.GetByIdAsync(reply.Id, default);
        loaded.Should().NotBeNull();
        loaded!.ThreadRootId.Should().Be(rootId);
        loaded.IsThreadReply.Should().BeTrue();
    }

    [Fact]
    public async Task IncrementThreadDenormAsync_OnRoot_UpdatesAllThreeFields()
    {
        var sender = Guid.NewGuid();
        var replier = Guid.NewGuid();
        var root = Message.Send(Guid.NewGuid(), ConvId, sender, "root", Array.Empty<Attachment>(),
            $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        await _repo.InsertAsync(root, default);

        var atUtc = DateTimeOffset.UtcNow.AddSeconds(10);
        var ok = await _repo.IncrementThreadDenormAsync(root.Id, replier, atUtc, default);

        ok.Should().BeTrue();
        var reloaded = await _repo.GetByIdAsync(root.Id, default);
        reloaded!.ThreadReplyCount.Should().Be(1);
        reloaded.ThreadParticipants.Should().ContainSingle().Which.Should().Be(replier);
        reloaded.ThreadLastReplyAtUtc.Should().NotBeNull();
        reloaded.ThreadLastReplyAtUtc!.Value.Should().BeCloseTo(atUtc, TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task IncrementThreadDenormAsync_DedupesParticipants_AcrossMultipleReplies()
    {
        var sender = Guid.NewGuid();
        var replier = Guid.NewGuid();
        var root = Message.Send(Guid.NewGuid(), ConvId, sender, "root", Array.Empty<Attachment>(),
            $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        await _repo.InsertAsync(root, default);

        await _repo.IncrementThreadDenormAsync(root.Id, replier, DateTimeOffset.UtcNow.AddSeconds(10), default);
        await _repo.IncrementThreadDenormAsync(root.Id, replier, DateTimeOffset.UtcNow.AddSeconds(20), default);
        await _repo.IncrementThreadDenormAsync(root.Id, replier, DateTimeOffset.UtcNow.AddSeconds(30), default);

        var reloaded = await _repo.GetByIdAsync(root.Id, default);
        reloaded!.ThreadReplyCount.Should().Be(3);
        reloaded.ThreadParticipants.Should().ContainSingle().Which.Should().Be(replier);
    }

    [Fact]
    public async Task IncrementThreadDenormAsync_OnReply_ReturnsFalse()
    {
        var sender = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var reply = Message.SendAsThreadReply(
            id: Guid.NewGuid(), conversationId: ConvId, senderId: sender, body: "r",
            attachments: Array.Empty<Attachment>(), clientMessageId: $"c-{Guid.NewGuid():N}",
            createdAtUtc: DateTimeOffset.UtcNow, threadRootId: rootId);
        await _repo.InsertAsync(reply, default);

        var ok = await _repo.IncrementThreadDenormAsync(reply.Id, sender, DateTimeOffset.UtcNow, default);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task ListThreadAsync_ReturnsRepliesInChronologicalOrder()
    {
        var sender = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var reply = Message.SendAsThreadReply(
                id: Guid.NewGuid(), conversationId: ConvId, senderId: sender, body: $"r{i}",
                attachments: Array.Empty<Attachment>(), clientMessageId: $"c-{i}-{Guid.NewGuid():N}",
                createdAtUtc: baseTime.AddSeconds(i), threadRootId: rootId);
            await _repo.InsertAsync(reply, default);
            ids.Add(reply.Id);
        }

        var page = await _repo.ListThreadAsync(rootId, cursor: null, limit: 10, CursorDirection.Older, default);

        page.Should().HaveCount(5);
        page.Select(m => m.Id).Should().Equal(ids[4], ids[3], ids[2], ids[1], ids[0]);
    }

    [Fact]
    public async Task ListByConversationAsync_ExcludesThreadReplies()
    {
        var sender = Guid.NewGuid();
        var conv = $"conv-mainflow-{Guid.NewGuid():N}";
        var root = Message.Send(Guid.NewGuid(), conv, sender, "root",
            Array.Empty<Attachment>(), $"c-r-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        await _repo.InsertAsync(root, default);

        // Add a thread reply
        var reply = Message.SendAsThreadReply(
            id: Guid.NewGuid(), conversationId: conv, senderId: sender, body: "in thread",
            attachments: Array.Empty<Attachment>(), clientMessageId: $"c-rep-{Guid.NewGuid():N}",
            createdAtUtc: DateTimeOffset.UtcNow.AddSeconds(10), threadRootId: root.Id);
        await _repo.InsertAsync(reply, default);

        var page = await _repo.ListByConversationAsync(conv, null, 100, CursorDirection.Older, default);

        page.Should().ContainSingle().Which.Id.Should().Be(root.Id);
    }

    [Fact]
    public async Task MarkReadUpToAsync_DoesNotReachIntoThreadReplies()
    {
        var sender = Guid.NewGuid();
        var conv = $"conv-read-thread-{Guid.NewGuid():N}";
        var root = Message.Send(Guid.NewGuid(), conv, sender, "root",
            Array.Empty<Attachment>(), $"c-r-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddMinutes(-1));
        await _repo.InsertAsync(root, default);

        var reply = Message.SendAsThreadReply(
            id: Guid.NewGuid(), conversationId: conv, senderId: sender, body: "reply",
            attachments: Array.Empty<Attachment>(), clientMessageId: $"c-rep-{Guid.NewGuid():N}",
            createdAtUtc: DateTimeOffset.UtcNow.AddSeconds(-30), threadRootId: root.Id);
        await _repo.InsertAsync(reply, default);

        await _repo.MarkReadUpToAsync(conv, root.Id, DateTimeOffset.UtcNow, default);

        var loadedReply = await _repo.GetByIdAsync(reply.Id, default);
        // Reply must remain in Sent state — main-flow read does not touch thread replies.
        loadedReply!.State.Should().Be(MessageState.Sent);
    }
}
