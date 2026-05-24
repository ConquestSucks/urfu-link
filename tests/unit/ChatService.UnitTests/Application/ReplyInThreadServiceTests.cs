using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class ReplyInThreadServiceTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IThreadSubscriptionRepository _subscriptions = Substitute.For<IThreadSubscriptionRepository>();
    private readonly IMediaServiceClient _media = Substitute.For<IMediaServiceClient>();
    private readonly IIdempotencyStore _idempotency = Substitute.For<IIdempotencyStore>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    public ReplyInThreadServiceTests()
    {
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _subscriptions.UpsertAsync(Arg.Any<ThreadSubscription>(), Arg.Any<CancellationToken>())
            .Returns(new ThreadSubscriptionUpsertResult(WasCreated: true, ReasonEscalated: false));
        _subscriptions.GetSubscriberIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<IReadOnlyList<Guid>>(new List<Guid> { Author }));
        _messages.IncrementThreadDenormAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private ReplyInThreadService Build()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        var options = Options.Create(new ChatOptions { MaxMentionsPerMessage = 50 });
        var disciplineClient = Substitute.For<Urfu.Link.Services.Chat.Application.Disciplines.IDisciplineServiceClient>();
        var mentions = new Urfu.Link.Services.Chat.Application.Mentions.MentionResolver(disciplineClient);
        return new ReplyInThreadService(
            _conversations, _messages, _subscriptions, _media, _idempotency,
            dispatcher, _broadcaster, mentions, _clock, options);
    }

    private (Conversation conv, Message root) SeedRoot(DateTimeOffset rootCreatedAt)
    {
        var conv = Conversation.OpenDirect(Author, Peer, rootCreatedAt);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);

        var root = Message.Send(
            id: Guid.NewGuid(),
            conversationId: conv.Id,
            senderId: Author,
            body: "root",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c-root",
            createdAtUtc: rootCreatedAt);
        _messages.GetByIdAsync(root.Id, Arg.Any<CancellationToken>()).Returns(root);
        return (conv, root);
    }

    private static ReplyInThreadRequest NewRequest(Guid sender, Guid rootId, string body = "hello", string clientId = "c-1")
        => new(sender, rootId, body, Array.Empty<Guid>(), ReplyToMessageId: null, ClientMessageId: clientId);

    [Fact]
    public async Task ReplyAsync_RootMissing_ThrowsThreadRootNotFound()
    {
        _messages.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Build().ReplyAsync(NewRequest(Author, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<ChatThreadRootNotFoundException>();
    }

    [Fact]
    public async Task ReplyAsync_RootIsThreadReply_ThrowsCannotReplyToReply()
    {
        var (conv, _) = SeedRoot(_clock.GetUtcNow());
        var existingRoot = Message.Send(Guid.NewGuid(), conv.Id, Peer, "r", Array.Empty<Attachment>(), "c-x", _clock.GetUtcNow());
        var nestedRoot = Message.SendAsThreadReply(
            id: Guid.NewGuid(),
            conversationId: conv.Id,
            senderId: Peer,
            body: "reply",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c-y",
            createdAtUtc: _clock.GetUtcNow(),
            threadRootId: existingRoot.Id);
        _messages.GetByIdAsync(nestedRoot.Id, Arg.Any<CancellationToken>()).Returns(nestedRoot);

        var act = () => Build().ReplyAsync(NewRequest(Author, nestedRoot.Id), default);

        await act.Should().ThrowAsync<ChatThreadCannotReplyToReplyException>();
    }

    [Fact]
    public async Task ReplyAsync_NonParticipant_ThrowsAccessDenied()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        var act = () => Build().ReplyAsync(NewRequest(Stranger, root.Id), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task ReplyAsync_HappyPath_InsertsReplyWithThreadRootId()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        await Build().ReplyAsync(NewRequest(Author, root.Id), default);

        await _messages.Received().InsertAsync(
            Arg.Is<Message>(m => m.ThreadRootId == root.Id && m.IsThreadReply),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplyAsync_HappyPath_IncrementsRootDenorm()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        await Build().ReplyAsync(NewRequest(Author, root.Id), default);

        await _messages.Received().IncrementThreadDenormAsync(
            root.Id, Author, _clock.GetUtcNow(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplyAsync_HappyPath_AutoSubscribesReplierAsReplied()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        await Build().ReplyAsync(NewRequest(Author, root.Id), default);

        await _subscriptions.Received().UpsertAsync(
            Arg.Is<ThreadSubscription>(s =>
                s.RootMessageId == root.Id
                && s.UserId == Author
                && s.Reason == ThreadSubscriptionReason.Replied),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplyAsync_WithMentions_AutoSubscribesMentioned()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());
        var body = $"hi @{Peer:D}";

        await Build().ReplyAsync(NewRequest(Author, root.Id, body), default);

        await _subscriptions.Received().UpsertAsync(
            Arg.Is<ThreadSubscription>(s =>
                s.UserId == Peer
                && s.Reason == ThreadSubscriptionReason.Mentioned),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplyAsync_HappyPath_PublishesThreadReplyPostedEvent()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        await Build().ReplyAsync(NewRequest(Author, root.Id), default);

        var posted = _outbox.Captured.OfType<ChatThreadReplyPostedEvent>().Should().ContainSingle().Subject;
        posted.RootMessageId.Should().Be(root.Id);
        posted.SenderId.Should().Be(Author);
        posted.Subscribers.Should().Contain(Author);
    }

    [Fact]
    public async Task ReplyAsync_WithMentions_PublishesMentionEventWithThreadRootId()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());
        var body = $"hi @{Peer:D}";

        await Build().ReplyAsync(NewRequest(Author, root.Id, body), default);

        var mention = _outbox.Captured.OfType<ChatMentionCreatedEvent>().Should().ContainSingle().Subject;
        mention.ThreadRootId.Should().Be(root.Id);
        mention.MentionedUserIds.Should().Contain(Peer);
    }

    [Fact]
    public async Task ReplyAsync_NewSubscriber_PublishesSubscriptionChangedEvent()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        await Build().ReplyAsync(NewRequest(Author, root.Id), default);

        var changed = _outbox.Captured.OfType<ChatThreadSubscriptionChangedEvent>().Should().ContainSingle().Subject;
        changed.UserId.Should().Be(Author);
        changed.Subscribed.Should().BeTrue();
        changed.Reason.Should().Be(ThreadSubscriptionReason.Replied);
    }

    [Fact]
    public async Task ReplyAsync_BroadcastsThreadReplyReceived_AndThreadRootUpdated()
    {
        var (conv, root) = SeedRoot(_clock.GetUtcNow());

        await Build().ReplyAsync(NewRequest(Author, root.Id), default);

        await _broadcaster.Received().NotifyThreadReplyReceivedAsync(
            Arg.Any<IReadOnlyList<Guid>>(),
            root.Id,
            Arg.Any<MessageDto>(),
            Arg.Any<CancellationToken>());
        await _broadcaster.Received().NotifyThreadRootUpdatedAsync(
            Arg.Is<IReadOnlyList<Guid>>(p => p.SequenceEqual(conv.Participants)),
            conv.Id,
            root.Id,
            1,
            Arg.Any<IReadOnlyList<Guid>>(),
            _clock.GetUtcNow(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplyAsync_DuplicateClientMessageId_ReturnsPriorMessage()
    {
        var (_, root) = SeedRoot(_clock.GetUtcNow());

        var existing = Message.SendAsThreadReply(
            id: Guid.NewGuid(),
            conversationId: root.ConversationId,
            senderId: Author,
            body: "first",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c-1",
            createdAtUtc: _clock.GetUtcNow().AddSeconds(-1),
            threadRootId: root.Id);

        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _messages.FindByClientMessageIdAsync(Author, "c-1", Arg.Any<CancellationToken>()).Returns(existing);

        var dto = await Build().ReplyAsync(NewRequest(Author, root.Id, body: "second", clientId: "c-1"), default);

        dto.Id.Should().Be(existing.Id);
        await _messages.DidNotReceive().InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    private sealed class RecordingOutboxWriter : IOutboxWriter
    {
        public List<IIntegrationEvent> Captured { get; } = new();

        public ValueTask EnqueueAsync<TEvent>(string topic, IntegrationEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Captured.Add(envelope.Payload);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
