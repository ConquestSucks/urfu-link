using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubThreadsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubThreadsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReplyInThread_BroadcastsThreadReplyToReplier_AndThreadRootUpdatedToConvParticipants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var aliceThreadRoot = new TaskCompletionSource<(Guid RootId, int ReplyCount)>(TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<string, Guid, int, IReadOnlyList<Guid>, DateTimeOffset>("ThreadRootUpdated",
            (_, rootId, replyCount, _, _) => aliceThreadRoot.TrySetResult((rootId, replyCount)));

        var bobThreadReply = new TaskCompletionSource<(Guid RootId, MessageDto Reply)>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobConn.On<Guid, MessageDto>("ThreadReplyReceived", (rootId, reply) => bobThreadReply.TrySetResult((rootId, reply)));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var root = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "root",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        var reply = await bobConn.InvokeAsync<MessageDto>("ReplyInThread", new
        {
            RootMessageId = root.Id,
            Body = "in thread",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });

        var rootUpdate = await aliceThreadRoot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        rootUpdate.RootId.Should().Be(root.Id);
        rootUpdate.ReplyCount.Should().Be(1);

        var replyEvent = await bobThreadReply.Task.WaitAsync(TimeSpan.FromSeconds(5));
        replyEvent.RootId.Should().Be(root.Id);
        replyEvent.Reply.Id.Should().Be(reply.Id);
        replyEvent.Reply.ThreadRootId.Should().Be(root.Id);
    }

    [Fact]
    public async Task NonSubscriber_DoesNotReceiveThreadReplyReceived_UntilJoinThread()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var charlie = Guid.NewGuid();

        // Seed a 3-participant conversation directly so charlie is part of the chat without
        // having replied or been mentioned. Direct conversations only support 2 participants,
        // so we hydrate a Group conversation through the repository.
        await using var seedScope = _factory.Services.CreateAsyncScope();
        var convs = seedScope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var conv = Conversation.Hydrate(
            id: $"group-{Guid.NewGuid():N}",
            type: ConversationType.Group,
            participants: new[] { alice, bob, charlie },
            createdAtUtc: DateTimeOffset.UtcNow,
            lastMessageAtUtc: DateTimeOffset.UtcNow,
            lastMessagePreview: null);
        await convs.TryCreateAsync(conv, default);

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);
        await using var charlieConn = await TestChatHubClient.ConnectAsync(_factory, charlie);

        // Charlie tracks ThreadReplyReceived; should not fire before JoinThread.
        var charlieReplyEvents = 0;
        charlieConn.On<Guid, MessageDto>("ThreadReplyReceived", (_, _) =>
        {
            Interlocked.Increment(ref charlieReplyEvents);
        });

        // Alice posts root.
        var root = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "root",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });

        // Bob replies in thread → he becomes Replied subscriber.
        await bobConn.InvokeAsync<MessageDto>("ReplyInThread", new
        {
            RootMessageId = root.Id,
            Body = "first reply",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });

        // Give the broadcast a moment to fan out (it will reach Bob's connection but not Charlie's).
        await Task.Delay(500);
        charlieReplyEvents.Should().Be(0, "Charlie has not subscribed yet");

        // Charlie joins.
        await charlieConn.InvokeAsync("JoinThread", root.Id);

        // Set up Charlie's expectation for the next reply.
        var charlieReplyTcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        charlieConn.Remove("ThreadReplyReceived");
        charlieConn.On<Guid, MessageDto>("ThreadReplyReceived", (rootId, _) => charlieReplyTcs.TrySetResult(rootId));

        // Bob replies again — Charlie should now receive.
        await bobConn.InvokeAsync<MessageDto>("ReplyInThread", new
        {
            RootMessageId = root.Id,
            Body = "second reply",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });

        var receivedRootId = await charlieReplyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receivedRootId.Should().Be(root.Id);
    }
}
