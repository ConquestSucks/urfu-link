using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Chat.Application.Contracts;

namespace ChatService.IntegrationTests.Hub;

/// <summary>
/// Integration coverage for ChatHub.StartTyping / StopTyping. Until this PR the
/// only path to PresenceService was a direct PresenceHub call from clients,
/// which bypassed chat-side <c>IsParticipant</c> authorization. ChatHub now
/// fans out the signal via gRPC after verifying participation; SendMessage
/// also fires StopTyping after a successful send so the indicator clears
/// without the client having to remember.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ChatHubTypingTests(ChatServiceFactory factory) : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartTyping_fans_out_to_presence_service()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);

        await aliceConn.InvokeAsync("StartTyping", conv.Id);

        _factory.PresenceServiceClient.Records.Should()
            .ContainSingle(r => r.UserId == alice && r.ConversationId == conv.Id && r.IsTyping,
                "ChatHub.StartTyping must fan the signal out to PresenceService.SetTyping(isTyping=true).");
    }

    [Fact]
    public async Task StopTyping_fans_out_to_presence_service_with_isTyping_false()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);

        await aliceConn.InvokeAsync("StopTyping", conv.Id);

        _factory.PresenceServiceClient.Records.Should()
            .ContainSingle(r => r.UserId == alice && r.ConversationId == conv.Id && !r.IsTyping);
    }

    [Fact]
    public async Task StartTyping_rejects_non_participant()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);
        await using var strangerConn = await TestChatHubClient.ConnectAsync(_factory, stranger);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);

        var act = async () => await strangerConn.InvokeAsync("StartTyping", conv.Id);

        await act.Should().ThrowAsync<HubException>(
            "typing in a conversation you cannot see is a security boundary, not a no-op.");

        _factory.PresenceServiceClient.Records.Should().BeEmpty(
            "PresenceService must not receive a typing signal for a non-participant.");
    }

    [Fact]
    public async Task SendMessage_auto_clears_typing_indicator_for_sender()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);

        // Alice starts typing, then sends — the explicit StopTyping should not be needed.
        await aliceConn.InvokeAsync("StartTyping", conv.Id);
        _factory.PresenceServiceClient.Reset();

        await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hello",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });

        _factory.PresenceServiceClient.Records.Should()
            .ContainSingle(r => r.UserId == alice && r.ConversationId == conv.Id && !r.IsTyping,
                "SendMessageService must auto-stop the sender's typing indicator after a successful send.");
    }
}
