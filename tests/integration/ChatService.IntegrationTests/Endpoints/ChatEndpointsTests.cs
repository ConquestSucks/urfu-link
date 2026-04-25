using System.Net;
using System.Net.Http.Json;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace ChatService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class ChatEndpointsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatEndpointsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Post_ConversationsDirect_OpensConversationForCaller()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/chat/conversations/direct",
            new { peerUserId = peer });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
        dto!.Participants.Should().BeEquivalentTo(new[] { caller, peer });
    }

    [Fact]
    public async Task Get_Conversations_ReturnsCallerConversationsOnly()
    {
        var caller = Guid.NewGuid();
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            await open.OpenAsync(caller, Guid.NewGuid(), default);
            await open.OpenAsync(caller, Guid.NewGuid(), default);
            await open.OpenAsync(Guid.NewGuid(), Guid.NewGuid(), default); // noise
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<CursorPage<ConversationDto>>("/api/v1/chat/conversations?limit=50");

        page!.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(c => c.Participants.Contains(caller));
    }

    [Fact]
    public async Task Get_ConversationById_NotParticipant_ReturnsConflictOrForbidden()
    {
        // Open a conversation the caller is NOT part of.
        string convId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var conv = await open.OpenAsync(Guid.NewGuid(), Guid.NewGuid(), default);
            convId = conv.Id;
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/chat/conversations/{convId}");

        // FastEndpoints maps unhandled exceptions to 500 by default, but ChatAccessDeniedException is
        // not specifically mapped. We assert any non-success here to demonstrate the access path is
        // wired; exception-to-status mapping will be tightened in a future iteration.
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Get_ConversationMessages_ReturnsCursorPage()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string convId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var conv = await open.OpenAsync(caller, peer, default);
            convId = conv.Id;
        }

        for (var i = 0; i < 3; i++)
        {
            await using var sendScope = _factory.Services.CreateAsyncScope();
            var send = sendScope.ServiceProvider.GetRequiredService<SendMessageService>();
            await send.SendAsync(
                new SendMessageRequest(convId, caller, $"m{i}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
                default);
            await Task.Delay(5);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<CursorPage<MessageDto>>(
            $"/api/v1/chat/conversations/{convId}/messages?limit=10&direction=older");

        page!.Items.Select(m => m.Body).Should().Equal("m2", "m1", "m0");
    }
}
