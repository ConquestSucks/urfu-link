using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MessageRepositoryReadByTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private MessageRepository _repo = null!;
    private MongoIndexInitializer _indexes = null!;

    private const string ConvId = "conv-readby";

    public MessageRepositoryReadByTests(MongoFixture mongo)
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
    public async Task AddReadByAsync_NewReader_PersistsReceiptAndReturnsTrue()
    {
        var msg = await InsertNewAsync();
        var reader = Guid.NewGuid();
        var readAt = DateTimeOffset.UtcNow;

        var added = await _repo.AddReadByAsync(msg.Id, new ReadReceipt(reader, readAt), default);

        added.Should().BeTrue();
        var receipts = await _repo.GetReadReceiptsAsync(msg.Id, default);
        receipts.Should().ContainSingle(r => r.UserId == reader);
        receipts[0].ReadAtUtc.Should().BeCloseTo(readAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AddReadByAsync_SameReaderTwice_IsIdempotent()
    {
        var msg = await InsertNewAsync();
        var reader = Guid.NewGuid();
        await _repo.AddReadByAsync(msg.Id, new ReadReceipt(reader, DateTimeOffset.UtcNow), default);

        var second = await _repo.AddReadByAsync(msg.Id, new ReadReceipt(reader, DateTimeOffset.UtcNow.AddSeconds(5)), default);

        second.Should().BeFalse();
        var receipts = await _repo.GetReadReceiptsAsync(msg.Id, default);
        receipts.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddReadByAsync_OnDeletedMessage_ReturnsFalse()
    {
        var msg = await InsertNewAsync();
        await _repo.ApplyDeleteForEveryoneAsync(msg.Id, msg.SenderId, DateTimeOffset.UtcNow, default);

        var added = await _repo.AddReadByAsync(msg.Id, new ReadReceipt(Guid.NewGuid(), DateTimeOffset.UtcNow), default);

        added.Should().BeFalse();
    }

    [Fact]
    public async Task GetReadReceiptsAsync_MessageNotFound_ReturnsEmpty()
    {
        var receipts = await _repo.GetReadReceiptsAsync(Guid.NewGuid(), default);

        receipts.Should().BeEmpty();
    }
}
