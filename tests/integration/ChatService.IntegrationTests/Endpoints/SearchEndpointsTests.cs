using System.Net;
using System.Net.Http.Json;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace ChatService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class SearchEndpointsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public SearchEndpointsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_Search_ReturnsHitsOnlyFromCallerConversations()
    {
        var caller = Guid.NewGuid();
        var peerA = Guid.NewGuid();
        var peerB = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        string convA, convB, convStranger;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            convA = (await open.OpenAsync(caller, peerA, default)).Id;
            convB = (await open.OpenAsync(caller, peerB, default)).Id;
            convStranger = (await open.OpenAsync(stranger, Guid.NewGuid(), default)).Id;
        }

        await SendAsync(convA, caller, "квантовая запутанность интересна");
        await SendAsync(convB, caller, "квантовая теория струн");
        await SendAsync(convStranger, stranger, "квантовая физика чужая");

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<CursorPage<MessageSearchResultDto>>(
            "/api/v1/chat/search?q=квантовая&limit=50");

        page!.Items.Should().HaveCount(2);
        page.Items.Select(i => i.ConversationId).Should().BeEquivalentTo(new[] { convA, convB });
    }

    [Fact]
    public async Task Get_Search_ConversationIdOutsideMembership_ReturnsEmptyNotForbidden()
    {
        var caller = Guid.NewGuid();
        string strangerConv;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            strangerConv = (await open.OpenAsync(Guid.NewGuid(), Guid.NewGuid(), default)).Id;
        }

        await SendAsync(strangerConv, Guid.NewGuid(), "тайное сообщение");

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/chat/search?q=тайное&conversationId={strangerConv}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<CursorPage<MessageSearchResultDto>>();
        page!.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task Get_Search_QueryTooShort_Returns400(string query)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());

        using var client = _factory.CreateClient();
        var url = string.IsNullOrEmpty(query)
            ? "/api/v1/chat/search"
            : $"/api/v1/chat/search?q={query}";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Search_LimitOver100_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/chat/search?q=test&limit=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Search_FiltersBySender()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string conv;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            conv = (await open.OpenAsync(caller, peer, default)).Id;
        }

        await SendAsync(conv, caller, "общий термин раз");
        await SendAsync(conv, peer, "общий термин два");

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<CursorPage<MessageSearchResultDto>>(
            $"/api/v1/chat/search?q=общий&senderId={caller}");

        page!.Items.Should().ContainSingle();
        page.Items[0].SenderId.Should().Be(caller);
    }

    [Fact]
    public async Task Get_Search_PaginatesViaCursor()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string conv;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            conv = (await open.OpenAsync(caller, peer, default)).Id;
        }

        for (var i = 0; i < 6; i++)
        {
            await SendAsync(conv, caller, $"уникальное слово{i:D2} тест");
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var first = await client.GetFromJsonAsync<CursorPage<MessageSearchResultDto>>(
            "/api/v1/chat/search?q=тест&limit=3");
        first!.Items.Should().HaveCount(3);
        first.NextCursor.Should().NotBeNullOrEmpty();

        var second = await client.GetFromJsonAsync<CursorPage<MessageSearchResultDto>>(
            $"/api/v1/chat/search?q=тест&limit=3&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        second!.Items.Should().HaveCount(3);

        var firstIds = first.Items.Select(i => i.MessageId).ToHashSet();
        var secondIds = second.Items.Select(i => i.MessageId).ToHashSet();
        firstIds.Should().NotIntersectWith(secondIds);
    }

    [Fact]
    public async Task Get_Search_BadCursor_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());

        using var client = _factory.CreateClient();
        // Allowed scope must be non-empty for cursor to actually be decoded — seed one conv.
        var caller = Guid.NewGuid();
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            await open.OpenAsync(caller, Guid.NewGuid(), default);
        }
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        var response = await client.GetAsync("/api/v1/chat/search?q=test&cursor=not-base64!!");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Search_HighlightedSnippet_PresentForBodyMatches()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string conv;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            conv = (await open.OpenAsync(caller, peer, default)).Id;
        }

        var prefix = new string('x', 50);
        var suffix = new string('y', 50);
        await SendAsync(conv, caller, $"{prefix} оплата {suffix}");

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<CursorPage<MessageSearchResultDto>>(
            "/api/v1/chat/search?q=оплата");

        page!.Items.Should().ContainSingle();
        page.Items[0].HighlightedSnippet.Should().NotBeNullOrEmpty().And.Contain("оплата");
    }

    [Fact]
    public async Task Get_Search_OverRateLimit_Returns429()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            await open.OpenAsync(caller, peer, default);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();

        // Policy is 30 req / minute per user — 30 must succeed, the 31st must be 429.
        for (var i = 0; i < 30; i++)
        {
            var ok = await client.GetAsync("/api/v1/chat/search?q=anything");
            ok.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} should be inside the window");
        }

        var blocked = await client.GetAsync("/api/v1/chat/search?q=anything");
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.Should().ContainSingle(h => h.Key == "Retry-After");
    }

    private async Task SendAsync(string conversationId, Guid senderId, string body)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var msg = Message.Send(
            Guid.NewGuid(),
            conversationId,
            senderId,
            body,
            Array.Empty<Attachment>(),
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow);
        await repo.InsertAsync(msg, default);
    }
}
