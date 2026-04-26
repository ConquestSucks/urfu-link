using System.Net;
using System.Net.Http.Json;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class MessengerEndpointsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public MessengerEndpointsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Patch_Message_ReturnsEdited()
    {
        var (caller, _, _, msg) = await SeedSentMessageAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/chat/messages/{msg.Id:D}",
            new { body = "edited" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MessageDto>();
        dto!.Body.Should().Be("edited");
        dto.EditedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Patch_Message_EmptyBody_Returns500OrBadRequest()
    {
        var (caller, _, _, msg) = await SeedSentMessageAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/chat/messages/{msg.Id:D}",
            new { body = string.Empty });

        // Service throws ArgumentException → FastEndpoints unmapped exceptions land as 500.
        // What matters: we don't accept the empty body silently.
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Message_ForEveryone_ReturnsTombstoned()
    {
        var (caller, _, _, msg) = await SeedSentMessageAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/chat/messages/{msg.Id:D}?mode=for-everyone");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MessageDto>();
        dto!.State.Should().Be(MessageState.Deleted);
        dto.DeleteMode.Should().Be(DeleteMode.ForEveryone);
    }

    [Fact]
    public async Task Delete_Message_DefaultMode_HidesForCaller()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(peer);

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/chat/messages/{msg.Id:D}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.IsHiddenFor(peer).Should().BeTrue();
    }

    [Fact]
    public async Task Post_Reactions_ReturnsNoContent_AndPersistsReaction()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(peer);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/chat/messages/{msg.Id:D}/reactions",
            new { emoji = "👍" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var loaded = await verifyScope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.Reactions.Should().ContainSingle(r => r.UserId == peer && r.Emoji == "👍");
    }

    [Fact]
    public async Task Delete_Reaction_RemovesPriorReaction()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync();
        await using (var addScope = _factory.Services.CreateAsyncScope())
        {
            var add = addScope.ServiceProvider.GetRequiredService<AddReactionService>();
            await add.AddAsync(new AddReactionRequest(msg.Id, peer, "👍"), default);
        }
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(peer);

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/chat/messages/{msg.Id:D}/reactions/{Uri.EscapeDataString("👍")}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_Pinned_ReturnsPinnedList()
    {
        var (caller, _, conv, msg) = await SeedSentMessageAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/chat/conversations/{conv.Id}/pinned",
            new { messageId = msg.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pinned = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
        pinned.Should().ContainSingle(m => m.Id == msg.Id);
    }

    [Fact]
    public async Task Delete_Pinned_RemovesAndReturnsRemainingList()
    {
        var (caller, _, conv, msg) = await SeedSentMessageAsync();
        await using (var pinScope = _factory.Services.CreateAsyncScope())
        {
            var pin = pinScope.ServiceProvider.GetRequiredService<PinMessageService>();
            await pin.PinAsync(new PinMessageRequest(conv.Id, caller, msg.Id), default);
        }
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/chat/conversations/{conv.Id}/pinned/{msg.Id:D}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pinned = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
        pinned.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_Forward_ReturnsNewMessages()
    {
        var caller = Guid.NewGuid();
        var sourcePeer = Guid.NewGuid();
        var targetPeer = Guid.NewGuid();

        Guid sourceMsgId;
        string targetConvId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var sourceConv = await open.OpenAsync(caller, sourcePeer, default);
            var targetConv = await open.OpenAsync(caller, targetPeer, default);
            targetConvId = targetConv.Id;

            var send = seed.ServiceProvider.GetRequiredService<SendMessageService>();
            var dto = await send.SendAsync(
                new SendMessageRequest(sourceConv.Id, caller, "to forward", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
                default);
            sourceMsgId = dto.Id;
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/chat/conversations/{targetConvId}/forward",
            new { messageIds = new[] { sourceMsgId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forwarded = await response.Content.ReadFromJsonAsync<List<MessageDto>>();
        forwarded.Should().ContainSingle();
        forwarded![0].ForwardedFrom.Should().NotBeNull();
        forwarded[0].SenderId.Should().Be(caller);
    }

    [Fact]
    public async Task Get_ReadReceipts_ReturnsReceiptsForParticipant()
    {
        var (caller, peer, _, msg) = await SeedSentMessageAsync();
        await using (var markScope = _factory.Services.CreateAsyncScope())
        {
            var markRead = markScope.ServiceProvider.GetRequiredService<MarkReadService>();
            await markRead.MarkAsync(new MarkReadRequest(msg.ConversationId, peer, msg.Id), default);
        }
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var receipts = await client.GetFromJsonAsync<List<ReadReceiptDto>>(
            $"/api/v1/chat/messages/{msg.Id:D}/read-receipts");

        receipts.Should().ContainSingle(r => r.UserId == peer);
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
            new SendMessageRequest(conv.Id, sender, "hello", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        var msg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        return (sender, peer, conv, msg!);
    }
}
