using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class ForwardMessagesServiceTests
{
    private static readonly Guid Caller = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetPeer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SourcePeer = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IMediaServiceClient _media = Substitute.For<IMediaServiceClient>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private ForwardMessagesService Build(int maxForwardedMessages = 50)
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        var options = Options.Create(new ChatOptions { MaxForwardedMessages = maxForwardedMessages });
        return new ForwardMessagesService(_conversations, _messages, _media, options, dispatcher, _broadcaster, _clock);
    }

    private (Conversation target, Conversation source, Message message) Seed()
    {
        var target = Conversation.OpenDirect(Caller, TargetPeer, _clock.GetUtcNow());
        var source = Conversation.OpenDirect(Caller, SourcePeer, _clock.GetUtcNow().AddMinutes(-5));
        _conversations.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _conversations.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var msg = Message.Send(
            id: Guid.NewGuid(),
            conversationId: source.Id,
            senderId: SourcePeer,
            body: "to forward",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c-source",
            createdAtUtc: _clock.GetUtcNow().AddMinutes(-1));
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);

        return (target, source, msg);
    }

    [Fact]
    public async Task ForwardAsync_TooManyMessages_Throws()
    {
        var (target, _, _) = Seed();
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();

        var act = () => Build(maxForwardedMessages: 50)
            .ForwardAsync(new ForwardMessagesRequest(target.Id, Caller, ids), default);

        await act.Should().ThrowAsync<ChatForwardLimitExceededException>();
    }

    [Fact]
    public async Task ForwardAsync_TargetConversationMissing_Throws()
    {
        _conversations.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Conversation?)null);

        var act = () => Build().ForwardAsync(
            new ForwardMessagesRequest("missing", Caller, new[] { Guid.NewGuid() }), default);

        await act.Should().ThrowAsync<ConversationNotFoundException>();
    }

    [Fact]
    public async Task ForwardAsync_NonParticipantInTarget_ThrowsAccessDenied()
    {
        var (target, _, msg) = Seed();

        var act = () => Build().ForwardAsync(
            new ForwardMessagesRequest(target.Id, Stranger, new[] { msg.Id }), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task ForwardAsync_NonParticipantInSource_ThrowsAccessDenied()
    {
        var (target, source, msg) = Seed();
        // Replace source so the caller is NOT a participant.
        var foreignSource = Conversation.OpenDirect(SourcePeer, Stranger, _clock.GetUtcNow().AddMinutes(-5));
        _conversations.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(foreignSource);
        // The forwarded message claims to live in source.Id, so we keep that wiring.
        var foreignMsg = Message.Send(
            Guid.NewGuid(), foreignSource.Id, SourcePeer, "x", Array.Empty<Attachment>(), "c-x", _clock.GetUtcNow());
        _conversations.GetByIdAsync(foreignSource.Id, Arg.Any<CancellationToken>()).Returns(foreignSource);
        _messages.GetByIdAsync(foreignMsg.Id, Arg.Any<CancellationToken>()).Returns(foreignMsg);

        var act = () => Build().ForwardAsync(
            new ForwardMessagesRequest(target.Id, Caller, new[] { foreignMsg.Id }), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task ForwardAsync_HappyPath_InsertsMessages_PublishesEvents_AndBroadcasts()
    {
        var (target, _, msg) = Seed();

        var result = await Build().ForwardAsync(
            new ForwardMessagesRequest(target.Id, Caller, new[] { msg.Id }), default);

        result.Should().ContainSingle();
        await _messages.Received().InsertAsync(
            Arg.Is<Message>(m => m.ConversationId == target.Id && m.ForwardedFrom != null),
            Arg.Any<CancellationToken>());
        _outbox.Captured.OfType<ChatMessageSentEvent>().Should().ContainSingle();
        await _broadcaster.Received().NotifyMessageReceivedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<MessageDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForwardAsync_WithAttachments_GrantsAccessForAllAttachmentsToTargetParticipants()
    {
        var target = Conversation.OpenDirect(Caller, TargetPeer, _clock.GetUtcNow());
        var source = Conversation.OpenDirect(Caller, SourcePeer, _clock.GetUtcNow().AddMinutes(-5));
        _conversations.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _conversations.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var attachmentId = Guid.NewGuid();
        var msg = Message.Send(
            id: Guid.NewGuid(),
            conversationId: source.Id,
            senderId: SourcePeer,
            body: "with attach",
            attachments: new[] { new Attachment(attachmentId, AttachmentType.Image, null, "p.png", 10, "image/png") },
            clientMessageId: "c-attach",
            createdAtUtc: _clock.GetUtcNow().AddMinutes(-1));
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);

        await Build().ForwardAsync(new ForwardMessagesRequest(target.Id, Caller, new[] { msg.Id }), default);

        await _media.Received().GrantConversationAccessAsync(
            attachmentId,
            Arg.Is<IReadOnlyList<Guid>>(list => list.Count == 1 && list[0] == TargetPeer),
            target.Id,
            Caller,
            Arg.Any<CancellationToken>());
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
