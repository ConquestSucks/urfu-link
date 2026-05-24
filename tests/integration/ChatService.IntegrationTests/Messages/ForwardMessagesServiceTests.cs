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
public class ForwardMessagesServiceTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ForwardMessagesServiceTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ForwardAsync_FromDirectToDirect_InsertsMessages_PublishesSentEvents_AndKeepsOriginalConversationId()
    {
        var caller = Guid.NewGuid();
        var sourcePeer = Guid.NewGuid();
        var targetPeer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var source = await open.OpenAsync(caller, sourcePeer, default);
        var target = await open.OpenAsync(caller, targetPeer, default);
        var convRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        await convRepo.TryCreateAsync(target, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var sourceDto = await send.SendAsync(
            new SendMessageRequest(source.Id, sourcePeer, "to forward", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: caller),
            default);
        _factory.OutboxWriter.Clear();

        var forward = scope.ServiceProvider.GetRequiredService<ForwardMessagesService>();
        var produced = await forward.ForwardAsync(
            new ForwardMessagesRequest(target.Id, caller, new[] { sourceDto.Id }), default);

        produced.Should().ContainSingle();
        produced[0].SenderId.Should().Be(caller);
        produced[0].ForwardedFrom.Should().NotBeNull();
        produced[0].ForwardedFrom!.OriginalSenderId.Should().Be(sourcePeer);
        produced[0].ForwardedFrom!.OriginalConversationId.Should().Be(source.Id);

        var sentEvents = _factory.OutboxWriter.Published
            .Select(p => p.Payload).OfType<ChatMessageSentEvent>().ToList();
        sentEvents.Should().ContainSingle(e => e.ConversationId == target.Id);
    }

    [Fact]
    public async Task ForwardAsync_ToGroupConversation_HidesOriginalConversationId()
    {
        var caller = Guid.NewGuid();
        var sourcePeer = Guid.NewGuid();
        var groupMember = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var source = await open.OpenAsync(caller, sourcePeer, default);

        // Inject a group conversation directly (no service supports group creation in #211).
        var convRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var groupConv = Conversation.Hydrate(
            id: $"group-{Guid.NewGuid():N}",
            type: ConversationType.Group,
            participants: new[] { caller, groupMember },
            createdAtUtc: DateTimeOffset.UtcNow,
            lastMessageAtUtc: DateTimeOffset.UtcNow,
            lastMessagePreview: null);
        await convRepo.TryCreateAsync(groupConv, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var sourceDto = await send.SendAsync(
            new SendMessageRequest(source.Id, caller, "secret source", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: sourcePeer),
            default);

        var forward = scope.ServiceProvider.GetRequiredService<ForwardMessagesService>();
        var produced = await forward.ForwardAsync(
            new ForwardMessagesRequest(groupConv.Id, caller, new[] { sourceDto.Id }), default);

        produced.Should().ContainSingle();
        produced[0].ForwardedFrom.Should().NotBeNull();
        produced[0].ForwardedFrom!.OriginalConversationId.Should().BeNull();
        produced[0].ForwardedFrom!.OriginalSenderId.Should().Be(caller);
    }

    [Fact]
    public async Task ForwardAsync_WithAttachment_GrantsAccessToTargetParticipants()
    {
        var caller = Guid.NewGuid();
        var sourcePeer = Guid.NewGuid();
        var targetPeer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var source = await open.OpenAsync(caller, sourcePeer, default);
        var target = await open.OpenAsync(caller, targetPeer, default);
        var convRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        await convRepo.TryCreateAsync(target, default);

        var assetId = Guid.NewGuid();
        _factory.MediaServiceClient.RegisterAsset(assetId, ownerId: caller);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var sourceDto = await send.SendAsync(
            new SendMessageRequest(source.Id, caller, "see attached", new[] { assetId }, $"c-{Guid.NewGuid():N}", PeerUserId: sourcePeer),
            default);

        // The send already grants access to sourcePeer; the forward should also grant access
        // to the target participants (targetPeer).
        var forward = scope.ServiceProvider.GetRequiredService<ForwardMessagesService>();
        await forward.ForwardAsync(
            new ForwardMessagesRequest(target.Id, caller, new[] { sourceDto.Id }), default);

        _factory.MediaServiceClient.Grants.Should()
            .Contain(g => g.AssetId == assetId && g.ConversationId == target.Id
                && g.GrantedByUserId == caller
                && g.UserIds.SequenceEqual(new[] { targetPeer }));
    }

    [Fact]
    public async Task ForwardAsync_NonParticipantInSource_ThrowsAccessDenied()
    {
        var caller = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var foreignPeer = Guid.NewGuid();
        var targetPeer = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var foreignConv = await open.OpenAsync(stranger, foreignPeer, default);
        var target = await open.OpenAsync(caller, targetPeer, default);
        var convRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        await convRepo.TryCreateAsync(target, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var foreignMsg = await send.SendAsync(
            new SendMessageRequest(foreignConv.Id, stranger, "private", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: foreignPeer),
            default);

        var forward = scope.ServiceProvider.GetRequiredService<ForwardMessagesService>();
        var act = () => forward.ForwardAsync(
            new ForwardMessagesRequest(target.Id, caller, new[] { foreignMsg.Id }), default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }
}
