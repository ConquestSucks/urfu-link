using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class MarkDeliveredAndReadTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public MarkDeliveredAndReadTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MarkDelivered_TransitionsSentMessages_AndPublishesDeliveredEvent()
    {
        var (sender, recipient, conv) = await OpenConversationAsync();
        var messageIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            await using var sendScope = _factory.Services.CreateAsyncScope();
            var send = sendScope.ServiceProvider.GetRequiredService<SendMessageService>();
            var dto = await send.SendAsync(
                new SendMessageRequest(conv.Id, sender, $"m{i}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
                default);
            messageIds.Add(dto.Id);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var mark = scope.ServiceProvider.GetRequiredService<MarkDeliveredService>();
        var transitioned = await mark.MarkAsync(
            new MarkDeliveredRequest(conv.Id, recipient, messageIds), default);

        transitioned.Should().BeEquivalentTo(messageIds);
        var msgRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        foreach (var id in messageIds)
        {
            var loaded = await msgRepo.GetByIdAsync(id, default);
            loaded!.State.Should().Be(MessageState.Delivered);
        }
        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMessageDeliveredEvent>()
            .Where(e => e.ConversationId == conv.Id)
            .Should().HaveCount(messageIds.Count);
    }

    [Fact]
    public async Task MarkDelivered_NonParticipant_ThrowsAccessDenied()
    {
        var (_, _, conv) = await OpenConversationAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var mark = scope.ServiceProvider.GetRequiredService<MarkDeliveredService>();

        var act = () => mark.MarkAsync(
            new MarkDeliveredRequest(conv.Id, Guid.NewGuid(), new[] { Guid.NewGuid() }),
            default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task MarkRead_TransitionsAllPriorMessages_AndPublishesReadEventOnce()
    {
        var (sender, recipient, conv) = await OpenConversationAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            await using var sendScope = _factory.Services.CreateAsyncScope();
            var send = sendScope.ServiceProvider.GetRequiredService<SendMessageService>();
            var dto = await send.SendAsync(
                new SendMessageRequest(conv.Id, sender, $"m{i}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
                default);
            ids.Add(dto.Id);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var mark = scope.ServiceProvider.GetRequiredService<MarkReadService>();
        var anchor = await mark.MarkAsync(
            new MarkReadRequest(conv.Id, recipient, ids[2]), default);

        anchor.Should().Be(ids[2]);

        var msgRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        for (var i = 0; i <= 2; i++)
        {
            var loaded = await msgRepo.GetByIdAsync(ids[i], default);
            loaded!.State.Should().Be(MessageState.Read);
        }
        var stillSent = await msgRepo.GetByIdAsync(ids[3], default);
        stillSent!.State.Should().Be(MessageState.Sent);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMessageReadEvent>()
            .Where(e => e.ConversationId == conv.Id)
            .Should().ContainSingle()
            .Which.MessageId.Should().Be(ids[2]);
    }

    [Fact]
    public async Task MarkRead_AllAlreadyRead_DoesNotRepublish()
    {
        var (sender, recipient, conv) = await OpenConversationAsync();
        await using var sendScope = _factory.Services.CreateAsyncScope();
        var send = sendScope.ServiceProvider.GetRequiredService<SendMessageService>();
        var msg = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "m", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mark = scope.ServiceProvider.GetRequiredService<MarkReadService>();
        await mark.MarkAsync(new MarkReadRequest(conv.Id, recipient, msg.Id), default);

        _factory.OutboxWriter.Clear();
        var second = await mark.MarkAsync(new MarkReadRequest(conv.Id, recipient, msg.Id), default);

        second.Should().BeNull();
        _factory.OutboxWriter.Published.Should().BeEmpty();
    }

    private async Task<(Guid sender, Guid recipient, Conversation conv)> OpenConversationAsync()
    {
        var sender = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var conv = Conversation.OpenDirect(sender, recipient, DateTimeOffset.UtcNow);
        var repo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        await repo.TryCreateAsync(conv, default);
        return (sender, recipient, conv);
    }
}
