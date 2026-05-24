using System.Globalization;
using FluentAssertions;
using NSubstitute;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Application.Users;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class SearchMessagesQueryTests
{
    private static readonly Guid Caller = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture);

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IUserServiceClient _users = Substitute.For<IUserServiceClient>();

    public SearchMessagesQueryTests()
    {
        _users.BatchGetUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, UserSummary>>(
                new Dictionary<Guid, UserSummary>()));
    }

    private SearchMessagesQuery Build() => new(_conversations, _messages, _users);

    [Fact]
    public async Task Execute_CallerHasNoConversations_ReturnsEmptyPage()
    {
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        var page = await Build().ExecuteAsync(
            new SearchMessagesParameters("term", null, null, null, null, null, null, null, null),
            Caller,
            default);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
        await _messages.DidNotReceive().SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConversationIdOutsideAllowedSet_ReturnsEmptyPageWithoutThrowing()
    {
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { "mine-1" });

        var page = await Build().ExecuteAsync(
            new SearchMessagesParameters("term", "stranger-conv", null, null, null, null, null, null, null),
            Caller,
            default);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
        await _messages.DidNotReceive().SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ConversationIdInsideAllowedSet_RestrictsScopeToThatId()
    {
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { "conv-a", "conv-b" });
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MessageSearchHit>());

        await Build().ExecuteAsync(
            new SearchMessagesParameters("term", "conv-a", null, null, null, null, null, null, null),
            Caller,
            default);

        await _messages.Received(1).SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Is<IReadOnlyList<string>>(s => s.Count == 1 && s[0] == "conv-a"),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_LimitDefaultsTo20_AndIsClampedTo100()
    {
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { "c1" });
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MessageSearchHit>());

        await Build().ExecuteAsync(
            new SearchMessagesParameters("term", null, null, null, null, null, null, null, Limit: null),
            Caller, default);
        await _messages.Received().SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            21, // 20 + 1 (lookahead)
            Arg.Any<CancellationToken>());

        await Build().ExecuteAsync(
            new SearchMessagesParameters("term", null, null, null, null, null, null, null, Limit: 999),
            Caller, default);
        await _messages.Received().SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            101, // 100 + 1
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MoreResultsThanLimit_TrimsAndIssuesNextCursor()
    {
        var conv = Conversation.OpenDirect(Caller, Peer, Now);
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { conv.Id });
        _conversations.GetByIdsAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { conv });

        var hits = Enumerable.Range(0, 6).Select(i => new MessageSearchHit(
            Message.Send(Guid.NewGuid(), conv.Id, Peer, $"термин {i}", Array.Empty<Attachment>(), $"client-{i}", Now.AddMinutes(-i)),
            6.0 - i)).ToList();
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            6,
            Arg.Any<CancellationToken>())
            .Returns(hits);

        var page = await Build().ExecuteAsync(
            new SearchMessagesParameters("термин", null, null, null, null, null, null, null, Limit: 5),
            Caller, default);

        page.Items.Should().HaveCount(5);
        page.NextCursor.Should().NotBeNullOrEmpty();
        var decoded = CursorCodec.DecodeMessageSearch(page.NextCursor);
        decoded.Should().NotBeNull();
        decoded!.Value.MessageId.Should().Be(hits[4].Message.Id);
        decoded.Value.Score.Should().Be(hits[4].Score);
    }

    [Fact]
    public async Task Execute_DirectPreview_SurfacesPeerUserId()
    {
        var conv = Conversation.OpenDirect(Caller, Peer, Now);
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { conv.Id });
        _conversations.GetByIdsAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { conv });

        var msg = Message.Send(Guid.NewGuid(), conv.Id, Peer, "текст", Array.Empty<Attachment>(), "c1", Now);
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new[] { new MessageSearchHit(msg, 1.0) });

        var page = await Build().ExecuteAsync(
            new SearchMessagesParameters("текст", null, null, null, null, null, null, null, null),
            Caller, default);

        page.Items.Should().ContainSingle();
        var preview = page.Items[0].ConversationPreview;
        preview.Type.Should().Be(ConversationType.Direct);
        preview.PeerUserId.Should().Be(Peer);
        preview.Title.Should().BeNull();
    }

    [Fact]
    public async Task Execute_SearchHit_EnrichesSenderNameAndAvatar()
    {
        var conv = Conversation.OpenDirect(Caller, Peer, Now);
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { conv.Id });
        _conversations.GetByIdsAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { conv });

        var msg = Message.Send(Guid.NewGuid(), conv.Id, Peer, "текст", Array.Empty<Attachment>(), "c1", Now);
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new[] { new MessageSearchHit(msg, 1.0) });
        _users.BatchGetUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, UserSummary>>(
                new Dictionary<Guid, UserSummary>
                {
                    [Peer] = new(Peer, "Peer User", "https://example.test/avatar.png", "peer@example.test"),
                }));

        var page = await Build().ExecuteAsync(
            new SearchMessagesParameters("текст", null, null, null, null, null, null, null, null),
            Caller, default);

        page.Items.Should().ContainSingle();
        var preview = page.Items[0].ConversationPreview;
        preview.SenderName.Should().Be("Peer User");
        preview.AvatarUrl.Should().Be("https://example.test/avatar.png");
        preview.Title.Should().Be("Peer User");
        await _users.Received(1).BatchGetUsersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(Peer)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_HighlightedSnippet_GeneratedForEachHit()
    {
        var conv = Conversation.OpenDirect(Caller, Peer, Now);
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { conv.Id });
        _conversations.GetByIdsAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { conv });

        var body = new string('x', 60) + " квантовая " + new string('y', 60);
        var msg = Message.Send(Guid.NewGuid(), conv.Id, Peer, body, Array.Empty<Attachment>(), "c1", Now);
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(new[] { new MessageSearchHit(msg, 1.0) });

        var page = await Build().ExecuteAsync(
            new SearchMessagesParameters("квантовая", null, null, null, null, null, null, null, null),
            Caller, default);

        page.Items.Should().ContainSingle()
            .Which.HighlightedSnippet.Should().Contain("квантовая");
    }

    [Fact]
    public async Task Execute_DecodesIncomingCursor_AndPassesItDownstream()
    {
        var conv = Conversation.OpenDirect(Caller, Peer, Now);
        _conversations.GetUserConversationIdsAsync(Caller, Arg.Any<CancellationToken>())
            .Returns(new[] { conv.Id });
        _messages.SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<MessageSearchCursor?>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MessageSearchHit>());

        var inbound = new MessageSearchCursor(2.5, Now, Guid.NewGuid());
        var encoded = CursorCodec.EncodeMessageSearch(inbound);

        await Build().ExecuteAsync(
            new SearchMessagesParameters("term", null, null, null, null, null, null, encoded, null),
            Caller, default);

        await _messages.Received(1).SearchAsync(
            Arg.Any<MessageSearchCriteria>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Is<MessageSearchCursor?>(c => c.HasValue && c.Value.MessageId == inbound.MessageId),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
