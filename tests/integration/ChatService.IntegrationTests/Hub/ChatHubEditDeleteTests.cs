using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubEditDeleteTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubEditDeleteTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EditMessage_BroadcastsMessageEditedToBothParticipants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var bobEdited = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobConn.On<MessageDto>("MessageEdited", msg => bobEdited.TrySetResult(msg));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "hi",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        await aliceConn.InvokeAsync<MessageDto>("EditMessage", new
        {
            MessageId = sent.Id,
            NewBody = "hi-edited",
        });

        var edited = await bobEdited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        edited.Id.Should().Be(sent.Id);
        edited.Body.Should().Be("hi-edited");
        edited.EditedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteMessage_ForEveryone_BroadcastsWireFormatMode()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var bobDeleted = new TaskCompletionSource<(string ConvId, Guid MessageId, string Mode, Guid DeletedBy)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bobConn.On<string, Guid, string, Guid>("MessageDeletedUpdate",
            (convId, messageId, mode, deletedBy) => bobDeleted.TrySetResult((convId, messageId, mode, deletedBy)));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "to delete",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        await aliceConn.InvokeAsync<MessageDto?>("DeleteMessage", sent.Id, "for-everyone");

        var deleted = await bobDeleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        deleted.MessageId.Should().Be(sent.Id);
        // Wire format must use kebab-case "for-everyone" — not the enum's ToString().
        deleted.Mode.Should().Be("for-everyone");
        deleted.DeletedBy.Should().Be(alice);
    }

    [Fact]
    public async Task DeleteMessage_ForMe_DoesNotBroadcastToOtherParticipants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var aliceDeleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<string, Guid, string, Guid>("MessageDeletedUpdate", (_, _, _, _) => aliceDeleted.TrySetResult(true));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "personal hide",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
            PeerUserId = bob,
        });

        // Bob hides locally; Alice must NOT receive a broadcast.
        await bobConn.InvokeAsync<MessageDto?>("DeleteMessage", sent.Id, "for-me");

        var fired = await Task.WhenAny(aliceDeleted.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        fired.Should().NotBe(aliceDeleted.Task);
    }
}
