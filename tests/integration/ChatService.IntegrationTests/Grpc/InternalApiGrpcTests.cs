using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using global::Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Grpc;

namespace ChatService.IntegrationTests.Grpc;

[Collection(IntegrationCollection.Name)]
public sealed class InternalApiGrpcTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;
    private HttpClient _httpClient = null!;
    private GrpcChannel _channel = null!;

    public InternalApiGrpcTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDataAsync();
        // Internal gRPC requires authentication; impersonate any user for the call.
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());
        _httpClient = _factory.CreateClient();
        _channel = GrpcChannel.ForAddress(_httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = _httpClient });
    }

    public Task DisposeAsync()
    {
        _channel.Dispose();
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Ping_ReturnsPongPlusEcho()
    {
        var client = new InternalApi.InternalApiClient(_channel);

        var reply = await client.PingAsync(new PingRequest { Message = "hello" });

        reply.Message.Should().Be("pong:hello");
        reply.Service.Should().Be("chat-service");
    }

    [Fact]
    public async Task GetConversationParticipants_ReturnsBothUsers()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var convId = await SeedDirectConversationAsync(alice, bob);

        var client = new InternalApi.InternalApiClient(_channel);
        var reply = await client.GetConversationParticipantsAsync(
            new GetConversationParticipantsRequest { ConversationId = convId });

        reply.UserIds.Should().BeEquivalentTo(new[] { alice.ToString("D"), bob.ToString("D") });
    }

    [Fact]
    public async Task IsParticipant_ReturnsTrueForMember_FalseForStranger()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var convId = await SeedDirectConversationAsync(alice, bob);

        var client = new InternalApi.InternalApiClient(_channel);

        var memberReply = await client.IsParticipantAsync(new IsParticipantRequest
        {
            ConversationId = convId,
            UserId = alice.ToString("D"),
        });
        memberReply.Participates.Should().BeTrue();
        memberReply.ConversationExists.Should().BeTrue();

        var strangerReply = await client.IsParticipantAsync(new IsParticipantRequest
        {
            ConversationId = convId,
            UserId = Guid.NewGuid().ToString("D"),
        });
        strangerReply.Participates.Should().BeFalse();
        strangerReply.ConversationExists.Should().BeTrue();
    }

    [Fact]
    public async Task IsParticipant_NonExistentConversation_ReturnsConversationDoesNotExist()
    {
        var client = new InternalApi.InternalApiClient(_channel);

        var reply = await client.IsParticipantAsync(new IsParticipantRequest
        {
            ConversationId = "deadbeef",
            UserId = Guid.NewGuid().ToString("D"),
        });

        reply.Participates.Should().BeFalse();
        reply.ConversationExists.Should().BeFalse();
    }

    [Fact]
    public async Task Ping_WithoutAuth_ReturnsUnauthenticated()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = new InternalApi.InternalApiClient(_channel);

        var act = () => client.PingAsync(new PingRequest()).ResponseAsync;

        var ex = await act.Should().ThrowAsync<global::Grpc.Core.RpcException>();
        ex.Which.StatusCode.Should().Be(global::Grpc.Core.StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task GetConversation_ReturnsTypeAndParticipants()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var convId = await SeedDirectConversationAsync(alice, bob);

        var client = new InternalApi.InternalApiClient(_channel);
        var reply = await client.GetConversationAsync(new GetConversationRequest { ConversationId = convId });

        reply.Exists.Should().BeTrue();
        reply.Type.Should().Be(ConversationKind.Direct);
        reply.Participants.Should().BeEquivalentTo(new[] { alice.ToString("D"), bob.ToString("D") });
    }

    private async Task<string> SeedDirectConversationAsync(Guid caller, Guid peer)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var draft = await open.OpenAsync(caller, peer, default);
        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        await send.SendAsync(
            new SendMessageRequest(
                draft.Id,
                caller,
                "seed",
                Array.Empty<Guid>(),
                $"grpc-{Guid.NewGuid():N}",
                PeerUserId: peer),
            default);
        return draft.Id;
    }
}
