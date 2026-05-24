using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class ReactionServicesTests
{
    private static readonly Guid Author = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private (Conversation conv, Message msg) Seed()
    {
        var conv = Conversation.OpenDirect(Author, Peer, _clock.GetUtcNow());
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        var msg = Message.Send(Guid.NewGuid(), conv.Id, Author, "x", Array.Empty<Attachment>(),
            "c1", _clock.GetUtcNow());
        _messages.GetByIdAsync(msg.Id, Arg.Any<CancellationToken>()).Returns(msg);
        return (conv, msg);
    }

    private AddReactionService BuildAdd(ChatOptions? options = null)
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        return new AddReactionService(
            _conversations, _messages, Options.Create(options ?? new ChatOptions()),
            dispatcher, _broadcaster, _clock);
    }

    private RemoveReactionService BuildRemove()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        return new RemoveReactionService(_conversations, _messages, dispatcher, _broadcaster, _clock);
    }

    [Fact]
    public async Task AddAsync_NonParticipant_Throws()
    {
        var (_, msg) = Seed();

        var act = () => BuildAdd().AddAsync(new AddReactionRequest(msg.Id, Stranger, "👍"), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task AddAsync_DisallowedEmoji_Throws()
    {
        var (_, msg) = Seed();
        var opts = new ChatOptions { AllowedReactionEmojis = new[] { "❤" } };

        var act = () => BuildAdd(opts).AddAsync(new AddReactionRequest(msg.Id, Author, "👍"), default);

        await act.Should().ThrowAsync<ChatReactionNotAllowedException>();
    }

    [Fact]
    public async Task AddAsync_HappyPath_PersistsAndPublishes()
    {
        var (_, msg) = Seed();
        _messages.AddReactionAsync(msg.Id, Arg.Any<Reaction>(), Arg.Any<CancellationToken>()).Returns(true);

        await BuildAdd().AddAsync(new AddReactionRequest(msg.Id, Author, "👍"), default);

        await _messages.Received().AddReactionAsync(msg.Id, Arg.Any<Reaction>(), Arg.Any<CancellationToken>());
        _outbox.Captured.OfType<ChatReactionAddedEvent>().Should().ContainSingle();
        await _broadcaster.Received().NotifyReactionUpdatedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), msg.Id, Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<Guid>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_AlreadySameReaction_DoesNotPublishOrBroadcast()
    {
        var (_, msg) = Seed();
        _messages.AddReactionAsync(msg.Id, Arg.Any<Reaction>(), Arg.Any<CancellationToken>()).Returns(false);

        await BuildAdd().AddAsync(new AddReactionRequest(msg.Id, Author, "👍"), default);

        _outbox.Captured.OfType<ChatReactionAddedEvent>().Should().BeEmpty();
        await _broadcaster.DidNotReceive().NotifyReactionUpdatedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(),
            Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<Guid>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_NonParticipant_Throws()
    {
        var (_, msg) = Seed();

        var act = () => BuildRemove().RemoveAsync(new RemoveReactionRequest(msg.Id, Stranger, "👍"), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task RemoveAsync_NotPresent_NoEventNoBroadcast()
    {
        var (_, msg) = Seed();
        _messages.RemoveReactionAsync(msg.Id, Author, "👍", Arg.Any<CancellationToken>()).Returns(false);

        await BuildRemove().RemoveAsync(new RemoveReactionRequest(msg.Id, Author, "👍"), default);

        _outbox.Captured.OfType<ChatReactionRemovedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_ExistingReaction_PublishesAndBroadcasts()
    {
        var (_, msg) = Seed();
        _messages.RemoveReactionAsync(msg.Id, Author, "👍", Arg.Any<CancellationToken>()).Returns(true);

        await BuildRemove().RemoveAsync(new RemoveReactionRequest(msg.Id, Author, "👍"), default);

        _outbox.Captured.OfType<ChatReactionRemovedEvent>().Should().ContainSingle();
        await _broadcaster.Received().NotifyReactionUpdatedAsync(
            Arg.Any<IReadOnlyList<Guid>>(), msg.Id,
            Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<Guid>>>(),
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
