using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class OpenDirectConversationServiceTests
{
    private readonly IConversationRepository _repo = Substitute.For<IConversationRepository>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();
    private readonly TimeProvider _clock = TimeProvider.System;

    private OpenDirectConversationService Build()
    {
        return new OpenDirectConversationService(_repo, _clock);
    }

    [Fact]
    public async Task OpenAsync_FirstTime_ReturnsDraftWithoutPersistingOrPublishing()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _repo.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Conversation?)null);

        var service = Build();
        var result = await service.OpenAsync(userA, userB, default);

        result.Participants.Should().BeEquivalentTo(new[] { userA, userB });
        result.LastMessagePreview.Should().BeNull();
        await _repo.DidNotReceive().TryCreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        _outbox.Captured.Should().BeEmpty();
        await _broadcaster.DidNotReceive().NotifyConversationUpdatedAsync(
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<Urfu.Link.Services.Chat.Application.Contracts.ConversationDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenAsync_ExistingConversation_ReturnsExisting_WithoutDoublePublish()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var existing = Conversation.OpenDirect(userA, userB, DateTimeOffset.UtcNow.AddDays(-1));
        _repo.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var service = Build();
        var result = await service.OpenAsync(userA, userB, default);

        result.Should().BeSameAs(existing);
        await _repo.DidNotReceive().TryCreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>());
        _outbox.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenAsync_OrderOfArguments_DoesNotMatter()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _repo.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Conversation?)null);

        var service = Build();
        var first = await service.OpenAsync(userA, userB, default);
        var second = await service.OpenAsync(userB, userA, default);

        second.Id.Should().Be(first.Id);
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
