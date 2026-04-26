using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MessageRepositoryTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private MessageRepository _repo = null!;
    private MongoIndexInitializer _indexes = null!;

    private const string ConvId = "conv-1";

    public MessageRepositoryTests(MongoFixture mongo)
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

    [Fact]
    public async Task InsertAsync_RoundTripsMessage()
    {
        var sender = Guid.NewGuid();
        var attachment = new Attachment(Guid.NewGuid(), AttachmentType.Image, null, "p.png", 100, "image/png");
        var msg = Message.Send(Guid.NewGuid(), ConvId, sender, "hi", new[] { attachment }, "client-1", DateTimeOffset.UtcNow);

        await _repo.InsertAsync(msg, default);

        var loaded = await _repo.GetByIdAsync(msg.Id, default);
        loaded.Should().NotBeNull();
        loaded!.Body.Should().Be("hi");
        loaded.Attachments.Should().HaveCount(1).And.ContainSingle(a => a.MediaAssetId == attachment.MediaAssetId);
        loaded.State.Should().Be(MessageState.Sent);
    }

    [Fact]
    public async Task InsertAsync_DuplicateClientMessageId_ForSameSender_Throws()
    {
        var sender = Guid.NewGuid();
        var first = Message.Send(Guid.NewGuid(), ConvId, sender, "a", Array.Empty<Attachment>(), "client-dup", DateTimeOffset.UtcNow);
        var second = Message.Send(Guid.NewGuid(), ConvId, sender, "b", Array.Empty<Attachment>(), "client-dup", DateTimeOffset.UtcNow);

        await _repo.InsertAsync(first, default);

        var act = () => _repo.InsertAsync(second, default);

        await act.Should().ThrowAsync<DuplicateClientMessageException>();
    }

    [Fact]
    public async Task FindByClientMessageIdAsync_ReturnsMatch()
    {
        var sender = Guid.NewGuid();
        var msg = Message.Send(Guid.NewGuid(), ConvId, sender, "echo", Array.Empty<Attachment>(), "client-find", DateTimeOffset.UtcNow);
        await _repo.InsertAsync(msg, default);

        var found = await _repo.FindByClientMessageIdAsync(sender, "client-find", default);

        found.Should().NotBeNull();
        found!.Id.Should().Be(msg.Id);
    }

    [Fact]
    public async Task ListByConversationAsync_OlderDirection_PagesNewestFirst()
    {
        var sender = Guid.NewGuid();
        var conv = $"conv-list-{Guid.NewGuid():N}";
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var m = Message.Send(Guid.NewGuid(), conv, sender, $"m{i}", Array.Empty<Attachment>(), $"c{i}-{Guid.NewGuid():N}", baseTime.AddSeconds(i));
            await _repo.InsertAsync(m, default);
            ids.Add(m.Id);
        }

        var page = await _repo.ListByConversationAsync(conv, cursor: null, limit: 3, CursorDirection.Older, default);

        page.Select(m => m.Id).Should().Equal(ids[4], ids[3], ids[2]);

        var next = await _repo.ListByConversationAsync(
            conv,
            cursor: new MessageCursor(page[^1].CreatedAtUtc, page[^1].Id),
            limit: 3,
            CursorDirection.Older,
            default);
        next.Select(m => m.Id).Should().Equal(ids[1], ids[0]);
    }

    [Fact]
    public async Task MarkDeliveredAsync_OnlyTransitionsSentMessages_AndReturnsAffectedIds()
    {
        var sender = Guid.NewGuid();
        var conv = $"conv-mark-{Guid.NewGuid():N}";
        var sent = Message.Send(Guid.NewGuid(), conv, sender, "a", Array.Empty<Attachment>(), $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var alreadyDelivered = Message.Send(Guid.NewGuid(), conv, sender, "b", Array.Empty<Attachment>(), $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        alreadyDelivered.MarkDelivered(DateTimeOffset.UtcNow);
        await _repo.InsertAsync(sent, default);
        await _repo.InsertAsync(alreadyDelivered, default);

        var result = await _repo.MarkDeliveredAsync(conv, new[] { sent.Id, alreadyDelivered.Id }, DateTimeOffset.UtcNow, default);

        result.Should().ContainSingle().Which.Should().Be(sent.Id);
        var reloaded = await _repo.GetByIdAsync(sent.Id, default);
        reloaded!.State.Should().Be(MessageState.Delivered);
    }

    [Fact]
    public async Task MarkReadUpToAsync_TransitionsAllPriorMessages_AndReturnsTransitionedIdsInChronologicalOrder()
    {
        var sender = Guid.NewGuid();
        var conv = $"conv-read-{Guid.NewGuid():N}";
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        var messages = Enumerable.Range(0, 4).Select(i =>
            Message.Send(Guid.NewGuid(), conv, sender, $"m{i}", Array.Empty<Attachment>(), $"c{i}-{Guid.NewGuid():N}", baseTime.AddSeconds(i))).ToList();
        foreach (var m in messages)
        {
            await _repo.InsertAsync(m, default);
        }

        var anchorId = messages[2].Id;
        var result = await _repo.MarkReadUpToAsync(conv, anchorId, DateTimeOffset.UtcNow, default);

        result.Should().Equal(messages[0].Id, messages[1].Id, messages[2].Id);
        result[^1].Should().Be(anchorId);

        for (var i = 0; i <= 2; i++)
        {
            var reloaded = await _repo.GetByIdAsync(messages[i].Id, default);
            reloaded!.State.Should().Be(MessageState.Read);
            reloaded.ReadAtUtc.Should().NotBeNull();
            reloaded.DeliveredAtUtc.Should().NotBeNull();
        }

        var stillSent = await _repo.GetByIdAsync(messages[3].Id, default);
        stillSent!.State.Should().Be(MessageState.Sent);
    }

    [Fact]
    public async Task MarkReadUpToAsync_AllAlreadyRead_ReturnsEmpty()
    {
        var sender = Guid.NewGuid();
        var conv = $"conv-read-noop-{Guid.NewGuid():N}";
        var msg = Message.Send(Guid.NewGuid(), conv, sender, "x", Array.Empty<Attachment>(), $"c-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        await _repo.InsertAsync(msg, default);
        await _repo.MarkReadUpToAsync(conv, msg.Id, DateTimeOffset.UtcNow, default);

        var second = await _repo.MarkReadUpToAsync(conv, msg.Id, DateTimeOffset.UtcNow, default);

        second.Should().BeEmpty();
    }
}
