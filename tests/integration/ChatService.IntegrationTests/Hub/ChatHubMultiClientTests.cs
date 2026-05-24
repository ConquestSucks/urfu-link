using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Chat.Application.Contracts;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubMultiClientTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubMultiClientTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SendMessage_DeliversMessageReceivedToOtherParticipantInRealtime()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var bobReceived = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobConn.On<MessageDto>("MessageReceived", msg => bobReceived.TrySetResult(msg));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);

        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi bob",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        var received = await bobReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Id.Should().Be(sent.Id);
        received.Body.Should().Be("hi bob");
    }

    [Fact]
    public async Task MarkRead_PushesMessageReadUpdateToSender()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var aliceRead = new TaskCompletionSource<(string ConvId, Guid UpTo, Guid Reader)>(TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<string, Guid, Guid>("MessageReadUpdate", (convId, upTo, reader)
            => aliceRead.TrySetResult((convId, upTo, reader)));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var msg = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        await bobConn.InvokeAsync<Guid?>("MarkRead", conv.Id, msg.Id);

        var update = await aliceRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        update.ConvId.Should().Be(conv.Id);
        update.UpTo.Should().Be(msg.Id);
        update.Reader.Should().Be(bob);
    }

    [Fact]
    public async Task MarkDelivered_PushesMessageDeliveredUpdateToSender()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var aliceDelivered = new TaskCompletionSource<(string ConvId, IReadOnlyList<Guid> Ids, Guid Recipient)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<string, IReadOnlyList<Guid>, Guid>("MessageDeliveredUpdate", (convId, ids, recipient)
            => aliceDelivered.TrySetResult((convId, ids, recipient)));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var msg = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        await bobConn.InvokeAsync<IReadOnlyList<Guid>>("MarkDelivered", conv.Id, new[] { msg.Id });

        var update = await aliceDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        update.ConvId.Should().Be(conv.Id);
        update.Ids.Should().Equal(msg.Id);
        update.Recipient.Should().Be(bob);
    }

    [Fact]
    public async Task FirstMessage_PushesConversationUpdatedToBothParticipants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var aliceUpdated = new TaskCompletionSource<ConversationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bobUpdated = new TaskCompletionSource<ConversationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<ConversationDto>("ConversationUpdated", c => aliceUpdated.TrySetResult(c));
        bobConn.On<ConversationDto>("ConversationUpdated", c => bobUpdated.TrySetResult(c));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        var aliceFromHub = await aliceUpdated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var bobFromHub = await bobUpdated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        aliceFromHub.Id.Should().Be(conv.Id);
        bobFromHub.Id.Should().Be(conv.Id);
        bobFromHub.LastMessagePreview!.Body.Should().Be("hi");
    }
}
