using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

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
            new SendMessageRequest(conv.Id, sender, "hello", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: peer),
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
    public async Task SendAsync_FirstDirectMessage_MaterializesDraftConversation()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var draft = await open.OpenAsync(sender, peer, default);

        var before = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(draft.Id, default);
        before.Should().BeNull();

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(draft.Id, sender, "first", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: peer),
            default);

        var loaded = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(draft.Id, default);
        loaded.Should().NotBeNull();
        loaded!.LastMessagePreview!.Body.Should().Be("first");

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatConversationCreatedEvent>()
            .Should().ContainSingle(e => e.ConversationId == draft.Id);
        dto.ConversationId.Should().Be(draft.Id);
    }

    [Fact]
    public async Task SendAsync_DuplicateClientMessageId_ReturnsPriorMessageWithoutDoublePublish()
    {
        var (sender, _, conv) = await OpenConversationAsync();
        var clientMsgId = $"c-{Guid.NewGuid():N}";

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        var first = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "hi", Array.Empty<Guid>(), clientMsgId, PeerUserId: conv.Participants.Single(p => p != sender)),
            default);

        // The fake idempotency store returns true on every call (treats every key as fresh).
        // So duplicate detection must rely on the unique sparse index in MongoDB.
        var second = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "hi-again", Array.Empty<Guid>(), clientMsgId),
            default);

        second.Id.Should().Be(first.Id);
        second.Body.Should().Be("hi");
        _factory.OutboxWriter.Published
            .Where(p => p.Payload is ChatMessageSentEvent ev && ev.MessageId == first.Id)
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task SendAsync_AssetOwnedBySomeoneElse_Throws_AndDoesNotPersist()
    {
        var (sender, _, conv) = await OpenConversationAsync();
        var assetId = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        _factory.MediaServiceClient.RegisterAsset(assetId, ownerId: someoneElse, kind: AttachmentType.Image);

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        var act = () => send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "x", new[] { assetId }, $"c-{Guid.NewGuid():N}"),
            default);

        await act.Should().ThrowAsync<ChatAttachmentNotOwnedException>();
        _factory.OutboxWriter.Published
            .Where(p => p.Payload is ChatMessageSentEvent ev && ev.ConversationId == conv.Id)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WithAttachment_PersistsServerSideMetadata_AndGrantsAccess()
    {
        var (sender, peer, conv) = await OpenConversationAsync();
        var assetId = Guid.NewGuid();
        _factory.MediaServiceClient.RegisterAsset(
            assetId,
            ownerId: sender,
            kind: AttachmentType.Image,
            sizeBytes: 4096,
            mimeType: "image/png",
            fileName: "server-controlled.png");

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "look", new[] { assetId }, $"c-{Guid.NewGuid():N}"),
            default);

        var attachment = dto.Attachments.Should().ContainSingle().Subject;
        attachment.MediaAssetId.Should().Be(assetId);
        attachment.Type.Should().Be(AttachmentType.Image);
        attachment.Size.Should().Be(4096);
        attachment.MimeType.Should().Be("image/png");
        attachment.FileName.Should().Be("server-controlled.png");

        var loadedConv = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(conv.Id, default);
        loadedConv!.LastMessagePreview!.AttachmentTypes.Should().ContainSingle().Which.Should().Be(AttachmentType.Image);

        _factory.MediaServiceClient.Grants
            .Should().ContainSingle(g => g.AssetId == assetId
                && g.ConversationId == conv.Id
                && g.GrantedByUserId == sender
                && g.UserIds.SequenceEqual(new[] { peer }));
    }

    [Fact]
    public async Task SendAsync_WithVoiceAttachment_PersistsDurationAndPreviewType()
    {
        var (sender, _, conv) = await OpenConversationAsync();
        var assetId = Guid.NewGuid();
        _factory.MediaServiceClient.RegisterAsset(
            assetId,
            ownerId: sender,
            kind: AttachmentType.Voice,
            sizeBytes: 2048,
            mimeType: "audio/m4a",
            fileName: "voice.m4a",
            durationSeconds: 17);

        await using var scope = _factory.Services.CreateAsyncScope();
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();

        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, "", new[] { assetId }, $"c-{Guid.NewGuid():N}"),
            default);

        var attachment = dto.Attachments.Should().ContainSingle().Subject;
        attachment.Type.Should().Be(AttachmentType.Voice);
        attachment.DurationSeconds.Should().Be(17);

        var loadedConv = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(conv.Id, default);
        loadedConv!.LastMessagePreview!.AttachmentTypes.Should().ContainSingle().Which.Should().Be(AttachmentType.Voice);
    }

    private async Task<(Guid sender, Guid peer, Conversation conv)> OpenConversationAsync()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var conv = Conversation.OpenDirect(sender, peer, DateTimeOffset.UtcNow);
        var repo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        await repo.TryCreateAsync(conv, default);
        return (sender, peer, conv);
    }
}
