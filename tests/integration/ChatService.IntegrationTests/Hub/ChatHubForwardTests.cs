using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Chat.Application.Contracts;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubForwardTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubForwardTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ForwardMessages_DeliversMessageReceivedWithForwardedFromToTargetParticipant()
    {
        var caller = Guid.NewGuid();
        var sourcePeer = Guid.NewGuid();
        var targetPeer = Guid.NewGuid();

        await using var callerConn = await TestChatHubClient.ConnectAsync(_factory, caller);
        await using var sourceConn = await TestChatHubClient.ConnectAsync(_factory, sourcePeer);
        await using var targetConn = await TestChatHubClient.ConnectAsync(_factory, targetPeer);

        // Build source conversation by sending a message peer→caller, then open target.
        var sourceConv = await callerConn.InvokeAsync<ConversationDto>("OpenDirectConversation", sourcePeer);
        var sourceMsg = await sourceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = sourceConv.Id,
            Body = "from source",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });
        var targetConv = await callerConn.InvokeAsync<ConversationDto>("OpenDirectConversation", targetPeer);

        var targetReceived = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        targetConn.On<MessageDto>("MessageReceived", msg => targetReceived.TrySetResult(msg));

        await callerConn.InvokeAsync<IReadOnlyList<MessageDto>>("ForwardMessages", targetConv.Id, new[] { sourceMsg.Id });

        var received = await targetReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.SenderId.Should().Be(caller);
        received.ConversationId.Should().Be(targetConv.Id);
        received.ForwardedFrom.Should().NotBeNull();
        received.ForwardedFrom!.OriginalSenderId.Should().Be(sourcePeer);
    }
}
