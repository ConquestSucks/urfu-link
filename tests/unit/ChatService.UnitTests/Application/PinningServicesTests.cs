using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class PinningServicesTests
{
    private static readonly Guid Caller = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IDisciplineRoleResolver _roles = Substitute.For<IDisciplineRoleResolver>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private (Conversation conv, Message msg) Seed(bool canPin = true)
    {
        var conv = Conversation.OpenDirect(Caller, Peer, _clock.GetUtcNow());
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        var msg = Message.Send(Guid.NewGuid(), conv.Id, Caller, "x", Array.Empty<Attachment>(), "c", _clock.GetUtcNow());
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        _roles.CanPinAsync(Arg.Any<Guid>(), Arg.Any<bool>(), conv, Arg.Any<CancellationToken>()).Returns(canPin);
        return (conv, msg);
    }

    private PinMessageService BuildPin(int maxPinned = 5)
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        var options = Options.Create(new ChatOptions { MaxPinnedMessages = maxPinned });
        return new PinMessageService(_conversations, _messages, _roles, options, dispatcher, _broadcaster, _clock);
    }

    private UnpinMessageService BuildUnpin()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        return new UnpinMessageService(_conversations, _messages, _roles, dispatcher, _broadcaster, _clock);
    }

    [Fact]
    public async Task PinAsync_NonParticipant_Throws()
    {
        var (conv, msg) = Seed();

        var act = () => BuildPin().PinAsync(new PinMessageRequest(conv.Id, Stranger, false, msg.Id), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task PinAsync_AdminNotParticipant_AllowedWhenResolverApproves()
    {
        var (conv, msg) = Seed();
        _conversations.AddPinnedMessageAsync(conv.Id, msg.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>())
            .Returns(conv,
                Conversation.Hydrate(conv.Id, ConversationType.Direct, conv.Participants, conv.CreatedAtUtc,
                    conv.LastMessageAtUtc, conv.LastMessagePreview, new[] { msg.Id }));
        _messages.GetByIdsAsync(conv.Id, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { msg });

        var dtos = await BuildPin().PinAsync(
            new PinMessageRequest(conv.Id, Stranger, CallerIsAdmin: true, msg.Id), default);

        dtos.Should().ContainSingle();
        _outbox.Captured.OfType<ChatMessagePinnedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task PinAsync_DisciplineRoleDenies_Throws()
    {
        var (conv, msg) = Seed(canPin: false);

        var act = () => BuildPin().PinAsync(new PinMessageRequest(conv.Id, Caller, false, msg.Id), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task PinAsync_HappyPath_PublishesAndBroadcasts()
    {
        var (conv, msg) = Seed();
        _conversations.AddPinnedMessageAsync(conv.Id, msg.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>())
            .Returns(conv,
                Conversation.Hydrate(conv.Id, ConversationType.Direct, conv.Participants, conv.CreatedAtUtc,
                    conv.LastMessageAtUtc, conv.LastMessagePreview, new[] { msg.Id }));
        _messages.GetByIdsAsync(conv.Id, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { msg });

        var dtos = await BuildPin().PinAsync(new PinMessageRequest(conv.Id, Caller, false, msg.Id), default);

        dtos.Should().ContainSingle();
        _outbox.Captured.OfType<ChatMessagePinnedEvent>().Should().ContainSingle();
        await _broadcaster.Received().NotifyPinsUpdatedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), conv.Id, Arg.Any<IReadOnlyList<MessageDto>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PinAsync_AtCap_Throws()
    {
        var (conv, msg) = Seed();
        _conversations.AddPinnedMessageAsync(conv.Id, msg.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => BuildPin(maxPinned: 5).PinAsync(new PinMessageRequest(conv.Id, Caller, false, msg.Id), default);

        await act.Should().ThrowAsync<ChatPinLimitExceededException>();
    }

    [Fact]
    public async Task PinAsync_AlreadyPinned_IdempotentNoEvent()
    {
        var (conv, msg) = Seed();
        var withPinned = Conversation.Hydrate(conv.Id, ConversationType.Direct, conv.Participants, conv.CreatedAtUtc,
            conv.LastMessageAtUtc, conv.LastMessagePreview, new[] { msg.Id });
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(withPinned);
        _roles.CanPinAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<Conversation>(), Arg.Any<CancellationToken>()).Returns(true);
        _messages.GetByIdsAsync(conv.Id, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { msg });

        await BuildPin().PinAsync(new PinMessageRequest(conv.Id, Caller, false, msg.Id), default);

        _outbox.Captured.OfType<ChatMessagePinnedEvent>().Should().BeEmpty();
        await _conversations.DidNotReceive().AddPinnedMessageAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpinAsync_HappyPath_PublishesAndBroadcasts()
    {
        var (conv, msg) = Seed();
        _conversations.RemovePinnedMessageAsync(conv.Id, msg.Id, Arg.Any<CancellationToken>()).Returns(true);
        _messages.GetByIdsAsync(conv.Id, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Message>());

        var dtos = await BuildUnpin().UnpinAsync(new UnpinMessageRequest(conv.Id, Caller, false, msg.Id), default);

        dtos.Should().BeEmpty();
        _outbox.Captured.OfType<ChatMessageUnpinnedEvent>().Should().ContainSingle();
        await _broadcaster.Received().NotifyPinsUpdatedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), conv.Id, Arg.Any<IReadOnlyList<MessageDto>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpinAsync_NotPinned_NoEvent()
    {
        var (conv, msg) = Seed();
        _conversations.RemovePinnedMessageAsync(conv.Id, msg.Id, Arg.Any<CancellationToken>()).Returns(false);

        await BuildUnpin().UnpinAsync(new UnpinMessageRequest(conv.Id, Caller, false, msg.Id), default);

        _outbox.Captured.OfType<ChatMessageUnpinnedEvent>().Should().BeEmpty();
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
