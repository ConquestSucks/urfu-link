using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
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

public class DeleteMessageServiceTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private DeleteMessageService Build()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        var options = Options.Create(new ChatOptions { DeleteForEveryoneTtlHours = 48 });
        return new DeleteMessageService(_conversations, _messages, options, dispatcher, _broadcaster, _clock);
    }

    private (Conversation conversation, Message message) Seed(Guid sender, DateTimeOffset createdAt)
    {
        var conv = Conversation.OpenDirect(sender, Peer, createdAt);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);

        var msg = Message.Send(
            id: Guid.NewGuid(),
            conversationId: conv.Id,
            senderId: sender,
            body: "hello",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c1",
            createdAtUtc: createdAt);
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _messages.ApplyDeleteForEveryoneAsync(msg.Id, Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _messages.AddHiddenForAsync(msg.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return (conv, msg);
    }

    [Fact]
    public async Task DeleteAsync_MissingMessage_ReturnsNull()
    {
        _messages.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var dto = await Build().DeleteAsync(
            new DeleteMessageRequest(Guid.NewGuid(), Author, DeleteMode.ForEveryone), default);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonParticipant_ThrowsAccessDenied()
    {
        var (_, msg) = Seed(Author, _clock.GetUtcNow());

        var act = () => Build().DeleteAsync(new DeleteMessageRequest(msg.Id, Stranger, DeleteMode.ForEveryone), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task DeleteAsync_ForEveryone_NotAuthor_Throws()
    {
        var (_, msg) = Seed(Author, _clock.GetUtcNow());

        var act = () => Build().DeleteAsync(new DeleteMessageRequest(msg.Id, Peer, DeleteMode.ForEveryone), default);

        await act.Should().ThrowAsync<ChatNotMessageAuthorException>();
    }

    [Fact]
    public async Task DeleteAsync_ForEveryone_PastTtl_Throws()
    {
        var (_, msg) = Seed(Author, _clock.GetUtcNow().AddHours(-49));

        var act = () => Build().DeleteAsync(new DeleteMessageRequest(msg.Id, Author, DeleteMode.ForEveryone), default);

        await act.Should().ThrowAsync<ChatEditTtlExpiredException>();
    }

    [Fact]
    public async Task DeleteAsync_ForEveryone_HappyPath_TombstonesPublishesAndBroadcasts()
    {
        var (_, msg) = Seed(Author, _clock.GetUtcNow());

        var dto = await Build().DeleteAsync(
            new DeleteMessageRequest(msg.Id, Author, DeleteMode.ForEveryone), default);

        dto.Should().NotBeNull();
        await _messages.Received().ApplyDeleteForEveryoneAsync(
            msg.Id, Author, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _outbox.Captured.OfType<ChatMessageDeletedEvent>()
            .Should().ContainSingle(e => e.Mode == DeleteMode.ForEveryone);
        await _broadcaster.Received().NotifyMessageDeletedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<string>(), msg.Id, DeleteMode.ForEveryone, Author, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ForMe_AnyParticipant_HidesLocally_NoEventNoBroadcast()
    {
        var (_, msg) = Seed(Author, _clock.GetUtcNow());

        var dto = await Build().DeleteAsync(new DeleteMessageRequest(msg.Id, Peer, DeleteMode.ForMe), default);

        dto.Should().NotBeNull();
        await _messages.Received().AddHiddenForAsync(msg.Id, Peer, Arg.Any<CancellationToken>());

        // ForMe is a local-only hide — no integration event, no broadcast.
        _outbox.Captured.OfType<ChatMessageDeletedEvent>().Should().BeEmpty();
        await _broadcaster.DidNotReceive().NotifyMessageDeletedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<DeleteMode>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
