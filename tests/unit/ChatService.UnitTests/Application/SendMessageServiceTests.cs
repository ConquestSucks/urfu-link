using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class SendMessageServiceTests
{
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IMediaServiceClient _media = Substitute.For<IMediaServiceClient>();
    private readonly IIdempotencyStore _idempotency = Substitute.For<IIdempotencyStore>();
    private readonly RecordingOutboxWriter _outbox = new();

    private SendMessageService Build()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        return new SendMessageService(_conversations, _messages, _media, _idempotency, dispatcher, TimeProvider.System);
    }

    private Conversation SeedConversation()
    {
        var conv = Conversation.OpenDirect(Sender, Peer, DateTimeOffset.UtcNow);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        return conv;
    }

    [Fact]
    public async Task SendAsync_NonParticipant_ThrowsAccessDenied()
    {
        var conv = SeedConversation();
        var stranger = Guid.NewGuid();
        var request = new SendMessageRequest(conv.Id, stranger, "x", Array.Empty<Attachment>(), "c1");

        var act = () => Build().SendAsync(request, default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task SendAsync_ConversationMissing_ThrowsNotFound()
    {
        _conversations.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Conversation?)null);

        var act = () => Build().SendAsync(
            new SendMessageRequest("missing", Sender, "x", Array.Empty<Attachment>(), "c1"),
            default);

        await act.Should().ThrowAsync<ConversationNotFoundException>();
    }

    [Fact]
    public async Task SendAsync_HappyPath_PersistsAndPublishesSentEvent()
    {
        var conv = SeedConversation();
        var request = new SendMessageRequest(conv.Id, Sender, "hello", Array.Empty<Attachment>(), "c1");

        var dto = await Build().SendAsync(request, default);

        dto.Body.Should().Be("hello");
        dto.SenderId.Should().Be(Sender);

        await _messages.Received(1).InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
        await _conversations.Received(1).UpdateLastMessageAsync(
            conv.Id, Arg.Any<MessagePreview>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        _outbox.Captured.OfType<ChatMessageSentEvent>().Should().ContainSingle()
            .Which.Should().Match<ChatMessageSentEvent>(e =>
                e.SenderId == Sender && e.Recipients.Count == 1 && e.Recipients[0] == Peer);
    }

    [Fact]
    public async Task SendAsync_DuplicateClientMessageId_ReturnsPriorMessage_WithoutDoublePublish()
    {
        var conv = SeedConversation();
        var prior = Message.Send(Guid.NewGuid(), conv.Id, Sender, "old", Array.Empty<Attachment>(), "c-dup", DateTimeOffset.UtcNow);

        _idempotency.TryRegisterAsync(Arg.Is<string>(k => k.EndsWith(":c-dup", StringComparison.Ordinal)), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));
        _messages.FindByClientMessageIdAsync(Sender, "c-dup", Arg.Any<CancellationToken>()).Returns(prior);

        var dto = await Build().SendAsync(
            new SendMessageRequest(conv.Id, Sender, "new", Array.Empty<Attachment>(), "c-dup"),
            default);

        dto.Id.Should().Be(prior.Id);
        await _messages.DidNotReceive().InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
        _outbox.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_AttachmentNotOwned_ThrowsAttachmentException_BeforeInsert()
    {
        var conv = SeedConversation();
        var asset = Guid.NewGuid();
        _media.CheckOwnershipAsync(asset, Sender, Arg.Any<CancellationToken>()).Returns(false);

        var attach = new Attachment(asset, Urfu.Link.Services.Chat.Domain.Enums.AttachmentType.Image, null, "p.png", 1, "image/png");
        var request = new SendMessageRequest(conv.Id, Sender, "x", new[] { attach }, "c1");

        var act = () => Build().SendAsync(request, default);

        await act.Should().ThrowAsync<ChatAttachmentNotOwnedException>();
        await _messages.DidNotReceive().InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithAttachment_GrantsAccessToOtherParticipants()
    {
        var conv = SeedConversation();
        var asset = Guid.NewGuid();
        _media.CheckOwnershipAsync(asset, Sender, Arg.Any<CancellationToken>()).Returns(true);

        var attach = new Attachment(asset, Urfu.Link.Services.Chat.Domain.Enums.AttachmentType.Image, null, "p.png", 1, "image/png");
        var request = new SendMessageRequest(conv.Id, Sender, "look", new[] { attach }, "c1");

        await Build().SendAsync(request, default);

        await _media.Received(1).GrantConversationAccessAsync(
            asset,
            Arg.Is<IReadOnlyList<Guid>>(u => u.Count == 1 && u[0] == Peer),
            conv.Id,
            Sender,
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
}
