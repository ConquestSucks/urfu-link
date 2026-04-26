using System.Globalization;
using FluentAssertions;
using NSubstitute;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class GetThreadMessagesQueryTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture);

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();

    private GetThreadMessagesQuery Build() => new(_conversations, _messages);

    private (Conversation conv, Message root) SeedRoot()
    {
        var conv = Conversation.OpenDirect(Author, Peer, Now);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        var root = Message.Send(Guid.NewGuid(), conv.Id, Author, "root",
            Array.Empty<Attachment>(), "c-root", Now);
        _messages.GetByIdAsync(root.Id, Arg.Any<CancellationToken>()).Returns(root);
        return (conv, root);
    }

    [Fact]
    public async Task Execute_RootMissing_Throws()
    {
        _messages.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Build().ExecuteAsync(Guid.NewGuid(), Author, null, null, CursorDirection.Older, default);

        await act.Should().ThrowAsync<ChatThreadRootNotFoundException>();
    }

    [Fact]
    public async Task Execute_NonParticipant_ThrowsAccessDenied()
    {
        var (_, root) = SeedRoot();

        var act = () => Build().ExecuteAsync(root.Id, Stranger, null, null, CursorDirection.Older, default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task Execute_ReturnsMessages_AndEncodesCursorWhenMore()
    {
        var (_, root) = SeedRoot();
        var replies = Enumerable.Range(0, 51).Select(i => Message.SendAsThreadReply(
                id: Guid.NewGuid(),
                conversationId: root.ConversationId,
                senderId: Peer,
                body: $"r{i}",
                attachments: Array.Empty<Attachment>(),
                clientMessageId: $"c-{i}",
                createdAtUtc: Now.AddSeconds(i),
                threadRootId: root.Id))
            .ToList();
        _messages.ListThreadAsync(root.Id, null, 51, CursorDirection.Older, Arg.Any<CancellationToken>())
            .Returns(replies);

        var page = await Build().ExecuteAsync(root.Id, Author, null, 50, CursorDirection.Older, default);

        page.Items.Should().HaveCount(50);
        page.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_NoMore_NextCursorIsNull()
    {
        var (_, root) = SeedRoot();
        _messages.ListThreadAsync(root.Id, null, Arg.Any<int>(), CursorDirection.Older, Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        var page = await Build().ExecuteAsync(root.Id, Author, null, 50, CursorDirection.Older, default);

        page.NextCursor.Should().BeNull();
    }
}
