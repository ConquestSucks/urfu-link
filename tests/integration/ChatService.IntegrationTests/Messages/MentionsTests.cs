using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class MentionsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public MentionsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SendMessage_WithMention_PublishesBothMessageSentAndMentionCreated()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, $"hi @{peer:D}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        dto.Mentions.Should().Equal(peer);

        var sent = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMessageSentEvent>().Single(e => e.MessageId == dto.Id);
        sent.Mentions.Should().NotBeNull();
        sent.Mentions!.Should().Equal(peer);

        var mentioned = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMentionCreatedEvent>().Single(e => e.MessageId == dto.Id);
        mentioned.MentionedUserIds.Should().Equal(peer);
    }

    [Fact]
    public async Task SendMessage_EveryoneMention_ExpandsToAllParticipants()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "@everyone please review", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        dto.Mentions.Should().BeEquivalentTo(conv.Participants);

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        loaded!.Mentions.Should().BeEquivalentTo(conv.Participants);
    }

    [Fact]
    public async Task SendMessage_PlainText_DoesNotEmitMentionEvent()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);
        _factory.OutboxWriter.Clear();

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "no mentions here", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMentionCreatedEvent>().Should().BeEmpty();
    }
}
