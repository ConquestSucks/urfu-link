using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MessageRepositorySearchTests : IClassFixture<MongoFixture>, IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ChatMongoContext _context = null!;
    private MessageRepository _repo = null!;

    public MessageRepositorySearchTests(MongoFixture mongo)
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
    public async Task SearchAsync_ReturnsMatchesOnlyInAllowedConversations()
    {
        var conv1 = $"conv-{Guid.NewGuid():N}";
        var conv2 = $"conv-{Guid.NewGuid():N}";
        var conv3 = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();

        await Insert(conv1, sender, "квантовая физика интересна");
        await Insert(conv2, sender, "квантовая запутанность");
        await Insert(conv3, sender, "квантовая теория струн");

        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria("квантовая", null, null, null, null, null),
            new[] { conv1, conv2 },
            cursor: null,
            limit: 50,
            CancellationToken.None);

        hits.Should().HaveCount(2);
        hits.Select(h => h.Message.ConversationId).Should().BeEquivalentTo(new[] { conv1, conv2 });
    }

    [Fact]
    public async Task SearchAsync_ExcludesThreadReplies()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();

        var root = Message.Send(Guid.NewGuid(), conv, sender, "оплата зачёта", Array.Empty<Attachment>(), Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        await _repo.InsertAsync(root, default);

        var reply = Message.SendAsThreadReply(
            Guid.NewGuid(),
            conv,
            sender,
            body: "оплата прошла",
            Array.Empty<Attachment>(),
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            threadRootId: root.Id);
        await _repo.InsertAsync(reply, default);

        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria("оплата", null, null, null, null, null),
            new[] { conv },
            cursor: null,
            limit: 50,
            CancellationToken.None);

        hits.Should().HaveCount(1);
        hits[0].Message.Id.Should().Be(root.Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersBySenderId()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await Insert(conv, alice, "термин общий");
        await Insert(conv, bob, "термин общий");

        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria("термин", alice, null, null, null, null),
            new[] { conv },
            cursor: null,
            limit: 50,
            CancellationToken.None);

        hits.Should().HaveCount(1);
        hits[0].Message.SenderId.Should().Be(alice);
    }

    [Fact]
    public async Task SearchAsync_FiltersByDateRange()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();
        var anchor = new DateTimeOffset(2026, 04, 25, 10, 00, 00, TimeSpan.Zero);

        await Insert(conv, sender, "слово раз", anchor);
        await Insert(conv, sender, "слово два", anchor.AddHours(2));
        await Insert(conv, sender, "слово три", anchor.AddHours(4));

        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria(
                Query: "слово",
                SenderId: null,
                DateFrom: anchor.AddHours(1),
                DateTo: anchor.AddHours(3),
                HasAttachments: null,
                AttachmentType: null),
            new[] { conv },
            cursor: null,
            limit: 50,
            CancellationToken.None);

        hits.Should().HaveCount(1);
        hits[0].Message.Body.Should().Be("слово два");
    }

    [Fact]
    public async Task SearchAsync_FiltersByHasAttachments()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();
        var attachment = new Attachment(Guid.NewGuid(), AttachmentType.Image, null, "p.png", 100, "image/png");

        await Insert(conv, sender, "просто текст");
        await Insert(conv, sender, "текст с фото", attachments: new[] { attachment });

        var withAttachments = await _repo.SearchAsync(
            new MessageSearchCriteria("текст", null, null, null, HasAttachments: true, null),
            new[] { conv }, null, 50, default);

        var withoutAttachments = await _repo.SearchAsync(
            new MessageSearchCriteria("текст", null, null, null, HasAttachments: false, null),
            new[] { conv }, null, 50, default);

        withAttachments.Should().HaveCount(1);
        withAttachments[0].Message.Body.Should().Contain("фото");

        withoutAttachments.Should().HaveCount(1);
        withoutAttachments[0].Message.Body.Should().Be("просто текст");
    }

    [Fact]
    public async Task SearchAsync_FiltersByAttachmentType()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();
        var image = new Attachment(Guid.NewGuid(), AttachmentType.Image, null, "p.png", 100, "image/png");
        var doc = new Attachment(Guid.NewGuid(), AttachmentType.Document, null, "f.pdf", 200, "application/pdf");

        await Insert(conv, sender, "содержимое раз", attachments: new[] { image });
        await Insert(conv, sender, "содержимое два", attachments: new[] { doc });

        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria("содержимое", null, null, null, null, AttachmentType.Image),
            new[] { conv }, null, 50, default);

        hits.Should().HaveCount(1);
        hits[0].Message.Attachments.Should().ContainSingle(a => a.Type == AttachmentType.Image);
    }

    [Fact]
    public async Task SearchAsync_OrdersByScoreThenCreatedAtDesc()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();
        var anchor = DateTimeOffset.UtcNow.AddDays(-1);

        // The phrase "карта" appears once in the first message, twice in the second — Mongo ranks
        // the second higher.
        await Insert(conv, sender, "карта одна", anchor);
        await Insert(conv, sender, "карта карта две", anchor.AddMinutes(1));
        await Insert(conv, sender, "карта три", anchor.AddMinutes(5));

        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria("карта", null, null, null, null, null),
            new[] { conv }, null, 50, default);

        hits.Should().HaveCount(3);
        hits[0].Message.Body.Should().Be("карта карта две");
        hits[0].Score.Should().BeGreaterThan(hits[1].Score);
    }

    [Fact]
    public async Task SearchAsync_PaginatesViaCursorWithoutDuplicates()
    {
        var conv = $"conv-{Guid.NewGuid():N}";
        var sender = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);

        for (var i = 0; i < 10; i++)
        {
            await Insert(conv, sender, $"уникальное слово{i:D2} тест", baseTime.AddSeconds(i));
        }

        var firstPage = await _repo.SearchAsync(
            new MessageSearchCriteria("тест", null, null, null, null, null),
            new[] { conv }, null, limit: 5, default);

        firstPage.Should().HaveCount(5);

        var lastHit = firstPage[^1];
        var cursor = new MessageSearchCursor(lastHit.Score, lastHit.Message.CreatedAtUtc, lastHit.Message.Id);

        var secondPage = await _repo.SearchAsync(
            new MessageSearchCriteria("тест", null, null, null, null, null),
            new[] { conv }, cursor, limit: 5, default);

        secondPage.Should().HaveCount(5);

        var firstIds = firstPage.Select(h => h.Message.Id).ToHashSet();
        var secondIds = secondPage.Select(h => h.Message.Id).ToHashSet();
        firstIds.Should().NotIntersectWith(secondIds);
        (firstIds.Count + secondIds.Count).Should().Be(10);
    }

    [Fact]
    public async Task SearchAsync_EmptyAllowedConversationIds_ReturnsEmpty()
    {
        var hits = await _repo.SearchAsync(
            new MessageSearchCriteria("anything", null, null, null, null, null),
            Array.Empty<string>(),
            cursor: null,
            limit: 50,
            default);

        hits.Should().BeEmpty();
    }

    private async Task Insert(
        string conversationId,
        Guid senderId,
        string body,
        DateTimeOffset? createdAtUtc = null,
        IReadOnlyList<Attachment>? attachments = null)
    {
        var message = Message.Send(
            Guid.NewGuid(),
            conversationId,
            senderId,
            body,
            attachments ?? Array.Empty<Attachment>(),
            Guid.NewGuid().ToString(),
            createdAtUtc ?? DateTimeOffset.UtcNow);
        await _repo.InsertAsync(message, default);
    }
}
