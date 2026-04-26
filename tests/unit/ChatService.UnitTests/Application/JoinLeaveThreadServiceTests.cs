using System.Globalization;
using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class JoinLeaveThreadServiceTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IThreadSubscriptionRepository _subscriptions = Substitute.For<IThreadSubscriptionRepository>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private ChatEventDispatcher Dispatcher() => new(
        _outbox,
        new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));

    private JoinThreadService BuildJoin() =>
        new(_conversations, _messages, _subscriptions, Dispatcher(), _broadcaster, _clock);

    private LeaveThreadService BuildLeave() => new(_subscriptions, Dispatcher(), _clock);

    private (Conversation conv, Message root) SeedRoot()
    {
        var conv = Conversation.OpenDirect(Author, Peer, _clock.GetUtcNow());
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        var root = Message.Send(
            id: Guid.NewGuid(),
            conversationId: conv.Id,
            senderId: Author,
            body: "root",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c-root",
            createdAtUtc: _clock.GetUtcNow());
        _messages.GetByIdAsync(root.Id, Arg.Any<CancellationToken>()).Returns(root);
        return (conv, root);
    }

    // ---- Join ----

    [Fact]
    public async Task Join_RootMissing_Throws()
    {
        _messages.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => BuildJoin().JoinAsync(new JoinThreadRequest(Peer, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<ChatThreadRootNotFoundException>();
    }

    [Fact]
    public async Task Join_NonParticipant_ThrowsAccessDenied()
    {
        var (_, root) = SeedRoot();

        var act = () => BuildJoin().JoinAsync(new JoinThreadRequest(Stranger, root.Id), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task Join_NewSubscription_PublishesEventAndBroadcasts()
    {
        var (_, root) = SeedRoot();
        _subscriptions.UpsertAsync(Arg.Any<ThreadSubscription>(), Arg.Any<CancellationToken>())
            .Returns(new ThreadSubscriptionUpsertResult(WasCreated: true, ReasonEscalated: false));
        _subscriptions.GetSubscriberIdsAsync(root.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Guid>>(new List<Guid> { Peer }));

        await BuildJoin().JoinAsync(new JoinThreadRequest(Peer, root.Id), default);

        var changed = _outbox.Captured.OfType<ChatThreadSubscriptionChangedEvent>().Should().ContainSingle().Subject;
        changed.UserId.Should().Be(Peer);
        changed.Subscribed.Should().BeTrue();
        changed.Reason.Should().Be(ThreadSubscriptionReason.Manual);

        await _broadcaster.Received().NotifyThreadParticipantJoinedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), root.Id, Peer, ThreadSubscriptionReason.Manual, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Join_AlreadyAtSameOrHigherReason_DoesNotPublish()
    {
        var (_, root) = SeedRoot();
        _subscriptions.UpsertAsync(Arg.Any<ThreadSubscription>(), Arg.Any<CancellationToken>())
            .Returns(new ThreadSubscriptionUpsertResult(WasCreated: false, ReasonEscalated: false));

        await BuildJoin().JoinAsync(new JoinThreadRequest(Peer, root.Id), default);

        _outbox.Captured.OfType<ChatThreadSubscriptionChangedEvent>().Should().BeEmpty();
        await _broadcaster.DidNotReceive().NotifyThreadParticipantJoinedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<ThreadSubscriptionReason>(), Arg.Any<CancellationToken>());
    }

    // ---- Leave ----

    [Fact]
    public async Task Leave_NoExistingSubscription_NoOp()
    {
        _subscriptions.RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await BuildLeave().LeaveAsync(new LeaveThreadRequest(Peer, Guid.NewGuid()), default);

        _outbox.Captured.OfType<ChatThreadSubscriptionChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Leave_RemovesAndPublishesUnsubscribedEvent()
    {
        var rootId = Guid.NewGuid();
        _subscriptions.RemoveAsync(rootId, Peer, Arg.Any<CancellationToken>()).Returns(true);

        await BuildLeave().LeaveAsync(new LeaveThreadRequest(Peer, rootId), default);

        var changed = _outbox.Captured.OfType<ChatThreadSubscriptionChangedEvent>().Should().ContainSingle().Subject;
        changed.RootMessageId.Should().Be(rootId);
        changed.UserId.Should().Be(Peer);
        changed.Subscribed.Should().BeFalse();
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
