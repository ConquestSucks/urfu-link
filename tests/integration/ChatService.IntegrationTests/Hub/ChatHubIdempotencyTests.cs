using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Events;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubIdempotencyTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubIdempotencyTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SendMessage_RepeatedWithSameClientMessageId_ReturnsSameMessageAndDoesNotDoubleBroadcast()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var clientMessageId = $"c-{Guid.NewGuid():N}";

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var bobReceivedCount = 0;
        bobConn.On<MessageDto>("MessageReceived", _ => Interlocked.Increment(ref bobReceivedCount));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var first = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = clientMessageId,
            PeerUserId = bob,
        });
        var second = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi-again",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = clientMessageId,
        });

        // Same logical message returned twice.
        second.Id.Should().Be(first.Id);
        second.Body.Should().Be("hi");

        // Outbox carries exactly one ChatMessageSentEvent for this message.
        _factory.OutboxWriter.Published
            .Where(p => p.Payload is ChatMessageSentEvent ev && ev.MessageId == first.Id)
            .Should().HaveCount(1);

        // Allow async broadcast to settle, then verify Bob got at most one MessageReceived for this id.
        await Task.Delay(200);
        bobReceivedCount.Should().BeLessThanOrEqualTo(1);
    }
}
