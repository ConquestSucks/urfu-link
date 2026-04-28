using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class EditMessageServiceTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private EditMessageService Build()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        var options = Options.Create(new ChatOptions { EditTtlHours = 48, MaxMentionsPerMessage = 50 });
        return new EditMessageService(_conversations, _messages, options, dispatcher, _broadcaster, _clock);
    }

    private (Conversation conversation, Message message) Seed(Guid sender, DateTimeOffset createdAt)
    {
        var conv = Conversation.OpenDirect(sender, Peer, createdAt);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);

        var msg = Message.Send(
            id: Guid.NewGuid(),
            conversationId: conv.Id,
            senderId: sender,
            body: "original",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "c1",
            createdAtUtc: createdAt);
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _messages.ApplyEditAsync(msg.Id, Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<EditHistoryEntry>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return (conv, msg);
    }

    [Fact]
    public async Task EditAsync_MissingMessage_Throws()
    {
        _messages.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Message?)null);

        var act = () => Build().EditAsync(new EditMessageRequest(Guid.NewGuid(), Author, "x"), default);

        await act.Should().ThrowAsync<ChatMessageNotFoundException>();
    }

    [Fact]
    public async Task EditAsync_NonParticipant_ThrowsAccessDenied()
    {
        var (conv, msg) = Seed(Author, _clock.GetUtcNow());

        var act = () => Build().EditAsync(new EditMessageRequest(msg.Id, Stranger, "x"), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task EditAsync_NotAuthor_ThrowsNotAuthor()
    {
        var (conv, msg) = Seed(Author, _clock.GetUtcNow());

        var act = () => Build().EditAsync(new EditMessageRequest(msg.Id, Peer, "x"), default);

        await act.Should().ThrowAsync<ChatNotMessageAuthorException>();
    }

    [Fact]
    public async Task EditAsync_PastTtl_ThrowsTtlExpired()
    {
        var createdAt = _clock.GetUtcNow().AddHours(-49);
        var (conv, msg) = Seed(Author, createdAt);

        var act = () => Build().EditAsync(new EditMessageRequest(msg.Id, Author, "edited"), default);

        await act.Should().ThrowAsync<ChatEditTtlExpiredException>();
    }

    [Fact]
    public async Task EditAsync_HappyPath_AppliesEdit_PublishesEvent_AndBroadcasts()
    {
        var (conv, msg) = Seed(Author, _clock.GetUtcNow());
        // Refresh path returns the same message instance (good enough for unit-level assertion).
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg, msg);

        var dto = await Build().EditAsync(new EditMessageRequest(msg.Id, Author, "edited"), default);

        dto.Should().NotBeNull();
        await _messages.Received().ApplyEditAsync(
            msg.Id, "edited", Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<EditHistoryEntry>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _outbox.Captured.OfType<ChatMessageEditedEvent>().Should().ContainSingle();
        await _broadcaster.Received().NotifyMessageEditedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<MessageDto>(), Arg.Any<CancellationToken>());
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
