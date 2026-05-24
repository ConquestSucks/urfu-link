using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Chat.Application.Contracts;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubReactionsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubReactionsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddReaction_BroadcastsReactionUpdatedSummaryToAllParticipants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var aliceSummary = new TaskCompletionSource<(Guid MessageId, IReadOnlyDictionary<string, IReadOnlyList<Guid>> Summary)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<Guid, IReadOnlyDictionary<string, IReadOnlyList<Guid>>>("ReactionUpdated",
            (msgId, summary) => aliceSummary.TrySetResult((msgId, summary)));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        await bobConn.InvokeAsync("AddReaction", sent.Id, "👍");

        var update = await aliceSummary.Task.WaitAsync(TimeSpan.FromSeconds(5));
        update.MessageId.Should().Be(sent.Id);
        update.Summary.Should().ContainKey("👍");
        update.Summary["👍"].Should().Equal(bob);
    }

    [Fact]
    public async Task RemoveReaction_BroadcastsEmptySummary()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        await bobConn.InvokeAsync("AddReaction", sent.Id, "👍");

        var aliceCleared = new TaskCompletionSource<IReadOnlyDictionary<string, IReadOnlyList<Guid>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<Guid, IReadOnlyDictionary<string, IReadOnlyList<Guid>>>("ReactionUpdated",
            (_, summary) => aliceCleared.TrySetResult(summary));

        await bobConn.InvokeAsync("RemoveReaction", sent.Id, "👍");

        var summary = await aliceCleared.Task.WaitAsync(TimeSpan.FromSeconds(5));
        summary.Should().BeEmpty();
    }
}
