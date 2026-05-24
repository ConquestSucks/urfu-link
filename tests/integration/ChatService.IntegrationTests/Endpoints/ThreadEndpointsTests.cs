using System.Net;
using System.Net.Http.Json;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;

namespace ChatService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class ThreadEndpointsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ThreadEndpointsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid alice, Guid bob, string convId, Guid rootId)> SeedThreadRootAsync()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        Guid rootId;
        string convId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var conv = await open.OpenAsync(alice, bob, default);
            convId = conv.Id;

            var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
            var sent = await send.SendAsync(
                new SendMessageRequest(
                    convId,
                    alice,
                    "root",
                    Array.Empty<Guid>(),
                    $"c-root-{Guid.NewGuid():N}",
                    PeerUserId: bob),
                default);
            rootId = sent.Id;
        }
        return (alice, bob, convId, rootId);
    }

    [Fact]
    public async Task Post_Reply_PostsReplyAndReturnsThreadRootId()
    {
        var (_, bob, _, rootId) = await SeedThreadRootAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(bob);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/chat/messages/{rootId}/thread",
            new
            {
                body = "in thread",
                attachmentAssetIds = Array.Empty<Guid>(),
                clientMessageId = $"c-{Guid.NewGuid():N}",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromChatJsonAsync<MessageDto>();
        dto!.ThreadRootId.Should().Be(rootId);
        dto.Body.Should().Be("in thread");
    }

    [Fact]
    public async Task Get_ThreadMessages_ReturnsRepliesPage()
    {
        var (_, bob, _, rootId) = await SeedThreadRootAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(bob);

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/v1/chat/messages/{rootId}/thread",
            new
            {
                body = "first",
                attachmentAssetIds = Array.Empty<Guid>(),
                clientMessageId = $"c-{Guid.NewGuid():N}",
            });

        var page = await client.GetFromChatJsonAsync<CursorPage<MessageDto>>(
            $"/api/v1/chat/messages/{rootId}/thread?limit=10");

        page!.Items.Should().ContainSingle().Which.Body.Should().Be("first");
    }

    [Fact]
    public async Task SubscribeAndUnsubscribe_RoundTrip_ReturnsNoContent()
    {
        var (_, bob, _, rootId) = await SeedThreadRootAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(bob);

        using var client = _factory.CreateClient();
        var subscribe = await client.PostAsJsonAsync(
            $"/api/v1/chat/messages/{rootId}/thread/subscribe",
            new { });
        subscribe.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unsubscribe = await client.DeleteAsync(
            $"/api/v1/chat/messages/{rootId}/thread/subscribe");
        unsubscribe.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Get_ActiveThreads_ListsRepliedSubscriptions()
    {
        var (_, bob, _, rootId) = await SeedThreadRootAsync();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(bob);

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/v1/chat/messages/{rootId}/thread",
            new
            {
                body = "first",
                attachmentAssetIds = Array.Empty<Guid>(),
                clientMessageId = $"c-{Guid.NewGuid():N}",
            });

        var page = await client.GetFromChatJsonAsync<CursorPage<ActiveThreadDto>>(
            "/api/v1/chat/threads/active?limit=10");

        page!.Items.Should().ContainSingle().Which.RootMessageId.Should().Be(rootId);
    }
}
