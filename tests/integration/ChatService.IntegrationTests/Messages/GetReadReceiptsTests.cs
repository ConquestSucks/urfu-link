using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class GetReadReceiptsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public GetReadReceiptsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecuteAsync_AfterMarkRead_ReturnsReceiptForReader()
    {
        var (sender, peer, _, msg) = await SeedSentMessageAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var markRead = scope.ServiceProvider.GetRequiredService<MarkReadService>();
        await markRead.MarkAsync(new MarkReadRequest(msg.ConversationId, peer, msg.Id), default);

        var query = scope.ServiceProvider.GetRequiredService<GetReadReceiptsQuery>();
        var receipts = await query.ExecuteAsync(msg.Id, sender, default);

        receipts.Should().ContainSingle(r => r.UserId == peer);
    }

    [Fact]
    public async Task ExecuteAsync_NonParticipant_ThrowsAccessDenied()
    {
        var (_, _, _, msg) = await SeedSentMessageAsync();
        var stranger = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<GetReadReceiptsQuery>();

        var act = () => query.ExecuteAsync(msg.Id, stranger, default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task ExecuteAsync_MissingMessage_ThrowsNotFound()
    {
        var caller = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<GetReadReceiptsQuery>();

        var act = () => query.ExecuteAsync(Guid.NewGuid(), caller, default);

        await act.Should().ThrowAsync<ChatMessageNotFoundException>();
    }

    [Fact]
    public async Task MarkRead_PopulatesReadByForAllTransitionedMessages()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();

        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var dto = await send.SendAsync(
                new SendMessageRequest(conv.Id, sender, $"m{i}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
                default);
            ids.Add(dto.Id);
        }

        // Reader marks the third message as read; all three should land in ReadBy.
        var markRead = scope.ServiceProvider.GetRequiredService<MarkReadService>();
        var anchor = await markRead.MarkAsync(new MarkReadRequest(conv.Id, peer, ids[^1]), default);
        anchor.Should().Be(ids[^1]);

        var repo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        foreach (var id in ids)
        {
            var receipts = await repo.GetReadReceiptsAsync(id, default);
            receipts.Should().ContainSingle(r => r.UserId == peer);
        }
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
            new SendMessageRequest(conv.Id, sender, "to read", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        var msg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        return (sender, peer, conv, msg!);
    }
}
