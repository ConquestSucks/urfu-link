using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Chat.Application.Contracts;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class ChatHubPinningTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubPinningTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PinMessage_BroadcastsPinsUpdatedWithFullPinnedList()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var bobPins = new TaskCompletionSource<(string ConvId, IReadOnlyList<MessageDto> Pinned)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bobConn.On<string, IReadOnlyList<MessageDto>>("PinsUpdated",
            (convId, pinned) => bobPins.TrySetResult((convId, pinned)));

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "important",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });

        await aliceConn.InvokeAsync<IReadOnlyList<MessageDto>>("PinMessage", conv.Id, sent.Id);

        var update = await bobPins.Task.WaitAsync(TimeSpan.FromSeconds(5));
        update.ConvId.Should().Be(conv.Id);
        update.Pinned.Should().ContainSingle(m => m.Id == sent.Id);
    }

    [Fact]
    public async Task UnpinMessage_BroadcastsEmptyPinsUpdated()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);
        await using var bobConn = await TestChatHubClient.ConnectAsync(_factory, bob);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);
        var sent = await aliceConn.InvokeAsync<MessageDto>("SendMessage", new
        {
            ConversationId = conv.Id,
            Body = "x",
            AttachmentAssetIds = Array.Empty<Guid>(),
            ClientMessageId = $"c-{Guid.NewGuid():N}",
        });
        await aliceConn.InvokeAsync<IReadOnlyList<MessageDto>>("PinMessage", conv.Id, sent.Id);

        var bobAfterUnpin = new TaskCompletionSource<IReadOnlyList<MessageDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobConn.On<string, IReadOnlyList<MessageDto>>("PinsUpdated", (_, pinned) => bobAfterUnpin.TrySetResult(pinned));

        await aliceConn.InvokeAsync<IReadOnlyList<MessageDto>>("UnpinMessage", conv.Id, sent.Id);

        var pinnedAfter = await bobAfterUnpin.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pinnedAfter.Should().BeEmpty();
    }
}
