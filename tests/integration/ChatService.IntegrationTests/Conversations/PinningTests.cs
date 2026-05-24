using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Conversations;

[Collection(IntegrationCollection.Name)]
public class PinningTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public PinningTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PinMessage_DirectConversation_AddsToPinnedListAndPublishesEvent()
    {
        var (sender, _, conv, msg) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var pin = scope.ServiceProvider.GetRequiredService<PinMessageService>();

        var pinned = await pin.PinAsync(new PinMessageRequest(conv.Id, sender, false, msg.Id), default);

        pinned.Should().ContainSingle(m => m.Id == msg.Id);
        var loaded = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(conv.Id, default);
        loaded!.PinnedMessageIds.Should().Contain(msg.Id);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMessagePinnedEvent>()
            .Should().ContainSingle(e => e.MessageId == msg.Id && e.PinnedByUserId == sender);
    }

    [Fact]
    public async Task PinMessage_AtCap_ThrowsLimit()
    {
        var (sender, peer, conv, _) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var convRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        for (var i = 0; i < 5; i++)
        {
            await convRepo.AddPinnedMessageAsync(conv.Id, Guid.NewGuid(), maxPinned: 5, default);
        }

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var sixthMsg = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "sixth", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: peer),
            default);

        var pin = scope.ServiceProvider.GetRequiredService<PinMessageService>();
        var act = () => pin.PinAsync(new PinMessageRequest(conv.Id, sender, false, sixthMsg.Id), default);

        await act.Should().ThrowAsync<ChatPinLimitExceededException>();
    }

    [Fact]
    public async Task PinMessage_GroupConversation_RejectedByDefaultDisciplineResolver()
    {
        var caller = Guid.NewGuid();
        var groupMember = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var convRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var groupConv = Conversation.Hydrate(
            id: $"group-{Guid.NewGuid():N}",
            type: ConversationType.Group,
            participants: new[] { caller, groupMember },
            createdAtUtc: DateTimeOffset.UtcNow,
            lastMessageAtUtc: DateTimeOffset.UtcNow,
            lastMessagePreview: null);
        await convRepo.TryCreateAsync(groupConv, default);

        // Tighten the fake to match production stub: only Direct conversations allow pinning.
        _factory.DisciplineRoleResolver.Predicate = (_, _, c) => c.Type == ConversationType.Direct;

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var msg = await send.SendAsync(
            new SendMessageRequest(groupConv.Id, caller, "in group", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        var pin = scope.ServiceProvider.GetRequiredService<PinMessageService>();
        var act = () => pin.PinAsync(new PinMessageRequest(groupConv.Id, caller, false, msg.Id), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task UnpinMessage_PinnedMessage_RemovesAndPublishes()
    {
        var (sender, _, conv, msg) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var pin = scope.ServiceProvider.GetRequiredService<PinMessageService>();
        await pin.PinAsync(new PinMessageRequest(conv.Id, sender, false, msg.Id), default);
        _factory.OutboxWriter.Clear();

        var unpin = scope.ServiceProvider.GetRequiredService<UnpinMessageService>();
        var pinnedAfter = await unpin.UnpinAsync(new UnpinMessageRequest(conv.Id, sender, false, msg.Id), default);

        pinnedAfter.Should().BeEmpty();
        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMessageUnpinnedEvent>()
            .Should().ContainSingle(e => e.MessageId == msg.Id);
    }

    private async Task<(Guid sender, Guid peer, Conversation conv, Message msg)> SeedSentMessageAsync()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();

        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "to pin", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: peer),
            default);

        var msg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        return (sender, peer, conv, msg!);
    }
}
