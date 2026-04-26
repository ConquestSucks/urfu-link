using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Realtime;

namespace ChatService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public sealed class ChatHubOnConnectedTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatHubOnConnectedTests(ChatServiceFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OnConnected_CallsListUserDisciplinesViaGrpc()
    {
        var userId = Guid.NewGuid();
        _factory.DisciplineServiceClient.Seed(userId,
            new UserDisciplineSnapshot(Guid.NewGuid(), "C1", "Course 1", ParticipantRole.Teacher));

        await using var conn = await TestChatHubClient.ConnectAsync(_factory, userId);

        _factory.DisciplineServiceClient.CalledForUsers.Should().Contain(userId);
    }

    [Fact]
    public async Task OnConnected_JoinsDisciplineGroup_BroadcastReachesConnection()
    {
        var userId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var conversationId = $"discipline:{disciplineId:N}";
        _factory.DisciplineServiceClient.Seed(userId,
            new UserDisciplineSnapshot(disciplineId, "C1", "Course 1", ParticipantRole.Teacher));

        await using var conn = await TestChatHubClient.ConnectAsync(_factory, userId);

        // Force a hub round-trip after connect so the LongPolling poll cycle is established
        // before we send the broadcast — without this the broadcast can race the first poll
        // and time out under TestServer's in-process transport.
        await conn.InvokeAsync<ConversationDto>("OpenDirectConversation", Guid.NewGuid());

        var archivedReceived = new TaskCompletionSource<(string ConvId, DateTimeOffset At)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        conn.On<string, DateTimeOffset>("ConversationArchived",
            (cid, at) => archivedReceived.TrySetResult((cid, at)));

        var hubContext = _factory.Services.GetRequiredService<IHubContext<ChatHub, IChatClient>>();
        await hubContext.Clients
            .Group(ChatHub.GroupNameFor(conversationId))
            .ConversationArchived(conversationId, DateTimeOffset.UtcNow);

        var got = await archivedReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        got.ConvId.Should().Be(conversationId);
    }

    [Fact]
    public async Task OpenDirectConversation_AddsCallerToGroup()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        await using var aliceConn = await TestChatHubClient.ConnectAsync(_factory, alice);

        var conv = await aliceConn.InvokeAsync<ConversationDto>("OpenDirectConversation", bob);

        var pinsReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        aliceConn.On<string, IReadOnlyList<MessageDto>>("PinsUpdated", (cid, _) => pinsReceived.TrySetResult(cid));

        var hubContext = _factory.Services.GetRequiredService<IHubContext<ChatHub, IChatClient>>();
        await hubContext.Clients.Group(ChatHub.GroupNameFor(conv.Id))
            .PinsUpdated(conv.Id, Array.Empty<MessageDto>());

        var receivedConvId = await pinsReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receivedConvId.Should().Be(conv.Id);
    }
}
