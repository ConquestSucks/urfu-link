using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class SendMessageServiceTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public SendMessageServiceTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SendAsync_PersistsMessage_UpdatesConversationPreview_AndPublishesEvent()
    {
        var (sender, peer, conv) = await OpenConversationAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "hello", Array.Empty<Attachment>(), $"c-{Guid.NewGuid():N}"),
            default);

        dto.Body.Should().Be("hello");

        var loadedConv = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(conv.Id, default);
        loadedConv!.LastMessagePreview.Should().NotBeNull();
        loadedConv.LastMessagePreview!.Body.Should().Be("hello");
        loadedConv.LastMessageAtUtc.Should().BeAfter(conv.LastMessageAtUtc);

        var loadedMsg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        loadedMsg.Should().NotBeNull();
        loadedMsg!.Body.Should().Be("hello");

        var sent = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatMessageSentEvent>()
            .Single(e => e.MessageId == dto.Id);
        sent.Recipients.Should().BeEquivalentTo(new[] { peer });
    }

    [Fact]
    public async Task SendAsync_DuplicateClientMessageId_ReturnsPriorMessageWithoutDoublePublish()
    {
        var (sender, _, conv) = await OpenConversationAsync();
        var clientMsgId = $"c-{Guid.NewGuid():N}";

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        var first = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "hi", Array.Empty<Attachment>(), clientMsgId),
            default);

        // The fake idempotency store returns true on every call (treats every key as fresh).
        // So duplicate detection must rely on the unique sparse index in MongoDB.
        var second = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "hi-again", Array.Empty<Attachment>(), clientMsgId),
            default);

        second.Id.Should().Be(first.Id);
        second.Body.Should().Be("hi");
        _factory.OutboxWriter.Published
            .Where(p => p.Payload is ChatMessageSentEvent ev && ev.MessageId == first.Id)
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task SendAsync_AttachmentNotOwned_Throws_AndDoesNotPersist()
    {
        var (sender, _, conv) = await OpenConversationAsync();
        var assetId = Guid.NewGuid();
        _factory.MediaServiceClient.SetOwnership(assetId, sender, isOwner: false);
        var attachment = new Attachment(assetId, AttachmentType.Image, null, "p.png", 100, "image/png");

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        var act = () => send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "x", new[] { attachment }, $"c-{Guid.NewGuid():N}"),
            default);

        await act.Should().ThrowAsync<ChatAttachmentNotOwnedException>();
        _factory.OutboxWriter.Published
            .Where(p => p.Payload is ChatMessageSentEvent ev && ev.ConversationId == conv.Id)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WithAttachment_GrantsAccessToOtherParticipants()
    {
        var (sender, peer, conv) = await OpenConversationAsync();
        var assetId = Guid.NewGuid();
        _factory.MediaServiceClient.SetOwnership(assetId, sender, isOwner: true);
        var attachment = new Attachment(assetId, AttachmentType.Image, null, "p.png", 100, "image/png");

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "look", new[] { attachment }, $"c-{Guid.NewGuid():N}"),
            default);

        _factory.MediaServiceClient.Grants
            .Should().ContainSingle(g => g.AssetId == assetId
                && g.ConversationId == conv.Id
                && g.GrantedByUserId == sender
                && g.UserIds.SequenceEqual(new[] { peer }));
    }

    private async Task<(Guid sender, Guid peer, Conversation conv)> OpenConversationAsync()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);
        return (sender, peer, conv);
    }
}
