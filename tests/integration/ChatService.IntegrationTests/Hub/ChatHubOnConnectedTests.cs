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
