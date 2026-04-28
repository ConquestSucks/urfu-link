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

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class DeleteMessageServiceTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public DeleteMessageServiceTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeleteAsync_ForEveryone_TombstonesMessage_AndPublishesEvent()
    {
        var (sender, _, conv, msg) = await SeedSentMessageAsync("hello");

        await using var scope = _factory.Services.CreateAsyncScope();
        var delete = scope.ServiceProvider.GetRequiredService<DeleteMessageService>();

        var dto = await delete.DeleteAsync(
            new DeleteMessageRequest(msg.Id, sender, DeleteMode.ForEveryone), default);

        dto.Should().NotBeNull();
        dto!.State.Should().Be(MessageState.Deleted);
        dto.DeleteMode.Should().Be(DeleteMode.ForEveryone);
        dto.Body.Should().BeEmpty();

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.State.Should().Be(MessageState.Deleted);
        loaded.DeletedBy.Should().Be(sender);

        var deleted = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatMessageDeletedEvent>()
            .Single(e => e.MessageId == msg.Id);
        deleted.Mode.Should().Be(DeleteMode.ForEveryone);
        deleted.ConversationId.Should().Be(conv.Id);
    }

    [Fact]
    public async Task DeleteAsync_ForMe_HidesLocally_NoEvent()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync("hello");
        _factory.OutboxWriter.Clear();

        await using var scope = _factory.Services.CreateAsyncScope();
        var delete = scope.ServiceProvider.GetRequiredService<DeleteMessageService>();

        var dto = await delete.DeleteAsync(
            new DeleteMessageRequest(msg.Id, peer, DeleteMode.ForMe), default);

        dto.Should().NotBeNull();
        // Other participants still see the original content.
        dto!.Body.Should().Be("hello");

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.IsHiddenFor(peer).Should().BeTrue();
        loaded.State.Should().NotBe(MessageState.Deleted);

        // Local hide must NOT show up on the wire.
        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatMessageDeletedEvent>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ForEveryone_ByNonAuthor_Throws()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync("hello");

        await using var scope = _factory.Services.CreateAsyncScope();
        var delete = scope.ServiceProvider.GetRequiredService<DeleteMessageService>();

        var act = () => delete.DeleteAsync(
            new DeleteMessageRequest(msg.Id, peer, DeleteMode.ForEveryone), default);

        await act.Should().ThrowAsync<ChatNotMessageAuthorException>();
    }

    [Fact]
    public async Task DeleteAsync_NonParticipant_Throws()
    {
        var (_, _, _, msg) = await SeedSentMessageAsync("hello");
        var stranger = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var delete = scope.ServiceProvider.GetRequiredService<DeleteMessageService>();

        var act = () => delete.DeleteAsync(
            new DeleteMessageRequest(msg.Id, stranger, DeleteMode.ForMe), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    private async Task<(Guid sender, Guid peer, Conversation conv, Message msg)> SeedSentMessageAsync(string body)
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();

        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, body, Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        var msg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        return (sender, peer, conv, msg!);
    }
}
