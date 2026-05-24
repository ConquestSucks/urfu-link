using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class ReactionsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ReactionsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddReaction_FromParticipant_PersistsAndPublishesEvent()
    {
        var (sender, peer, _, msg) = await SeedSentMessageAsync();
        _factory.OutboxWriter.Clear();

        await using var scope = _factory.Services.CreateAsyncScope();
        var add = scope.ServiceProvider.GetRequiredService<AddReactionService>();

        await add.AddAsync(new AddReactionRequest(msg.Id, peer, "👍"), default);

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().ContainSingle(r => r.UserId == peer && r.Emoji == "👍");

        var added = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatReactionAddedEvent>().Single();
        added.UserId.Should().Be(peer);
        added.Emoji.Should().Be("👍");

        // sender doesn't react in this scenario but is the conversation peer — sanity check.
        sender.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddReaction_SameEmojiTwice_IsNoop()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var add = scope.ServiceProvider.GetRequiredService<AddReactionService>();

        await add.AddAsync(new AddReactionRequest(msg.Id, peer, "👍"), default);
        _factory.OutboxWriter.Clear();
        await add.AddAsync(new AddReactionRequest(msg.Id, peer, "👍"), default);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatReactionAddedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddReaction_ChangingEmoji_Atomically_ReplacesPriorReaction()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var add = scope.ServiceProvider.GetRequiredService<AddReactionService>();

        await add.AddAsync(new AddReactionRequest(msg.Id, peer, "👍"), default);
        await add.AddAsync(new AddReactionRequest(msg.Id, peer, "❤"), default);

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().ContainSingle(r => r.UserId == peer);
        loaded.Reactions[0].Emoji.Should().Be("❤");
    }

    [Fact]
    public async Task RemoveReaction_ExistingReaction_RemovesAndPublishes()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var add = scope.ServiceProvider.GetRequiredService<AddReactionService>();
        await add.AddAsync(new AddReactionRequest(msg.Id, peer, "👍"), default);
        _factory.OutboxWriter.Clear();

        var remove = scope.ServiceProvider.GetRequiredService<RemoveReactionService>();
        await remove.RemoveAsync(new RemoveReactionRequest(msg.Id, peer, "👍"), default);

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().BeEmpty();
        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatReactionRemovedEvent>()
            .Should().ContainSingle(e => e.UserId == peer && e.Emoji == "👍");
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
            new SendMessageRequest(conv.Id, sender, "to react", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: peer),
            default);

        var msg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        return (sender, peer, conv, msg!);
    }
}
