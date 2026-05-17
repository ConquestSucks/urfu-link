using System.Net;
using System.Net.Http.Headers;
using ApiGateway.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests;

public sealed class RoutingIntegrationTests : IAsyncLifetime
{
    private StubDownstreamServer _userStub = null!;
    private StubDownstreamServer _chatStub = null!;
    private GatewayTestFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _userStub = await StubDownstreamServer.StartAsync();
        _chatStub = await StubDownstreamServer.StartAsync();

        _factory = new GatewayTestFactory(new Dictionary<string, string>
        {
            ["user-cluster"] = _userStub.BaseUrl,
            ["chat-cluster"] = _chatStub.BaseUrl,
        });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _chatStub.DisposeAsync();
        await _userStub.DisposeAsync();
    }

    [Fact]
    public async Task RestRequest_ForwardsCorrelationIdAndAuthorization_ToDownstream()
    {
        const string correlationId = "test-correlation-abc";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-42");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _userStub.Requests.Should().HaveCount(1);
        var recorded = _userStub.Requests.First();
        recorded.Path.Should().Be("/api/v1/me");
        recorded.Headers.Should().ContainKey("X-Correlation-Id")
            .WhoseValue.Should().Be(correlationId);
        recorded.Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer user-42");

        response.Headers.GetValues("X-Correlation-Id").Should().Contain(correlationId);
    }

    [Fact]
    public async Task RestRequest_GeneratesCorrelationId_WhenAbsentFromClient()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-42");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _userStub.Requests.Should().HaveCount(1);
        var recorded = _userStub.Requests.First();
        recorded.Headers.Should().ContainKey("X-Correlation-Id");
        recorded.Headers["X-Correlation-Id"].Should().NotBeNullOrWhiteSpace();
        response.Headers.GetValues("X-Correlation-Id").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RestRequest_WithoutAuth_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _userStub.Requests.Should().BeEmpty("authorization is enforced at the gateway for REST routes");
    }

    [Fact]
    public async Task DirectHubRoute_ChatNegotiate_ProxiesWithoutGatewayAuth()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/chat/negotiate?access_token=user-42")
        {
            Content = new StringContent(string.Empty),
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _chatStub.Requests.Should().HaveCount(1);
        var recorded = _chatStub.Requests.First();
        recorded.Path.Should().Be("/hubs/chat/negotiate");
        recorded.QueryString.Should().Contain("access_token=user-42");
    }

    [Fact]
    public async Task HubRoute_WithoutAccessToken_Returns401_BeforeDownstream()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/chat/negotiate")
        {
            Content = new StringContent(string.Empty),
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("Bearer");
        _chatStub.Requests.Should().BeEmpty(
            "anonymous hub traffic is rejected at the gateway before reaching downstream");
    }

    [Fact]
    public async Task HubRoute_WithBearerHeader_ProxiesToDownstream()
    {
        // SignalR JS-клиент на HTTP-фазе negotiate передаёт токен в Authorization header,
        // а не в query. Gateway-middleware обязан принимать оба источника, иначе валидный
        // запрос отвергается до того, как стандартный JWT-pipeline проверит подпись.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/chat/negotiate")
        {
            Content = new StringContent(string.Empty),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-42");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _chatStub.Requests.Should().HaveCount(1);
        var recorded = _chatStub.Requests.First();
        recorded.Path.Should().Be("/hubs/chat/negotiate");
        recorded.Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer user-42");
    }

    [Fact]
    public async Task DirectHubRoute_PresenceConnect_ProxiesWebSocketHandshake()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/presence/negotiate?access_token=user-42")
        {
            Content = new StringContent(string.Empty),
        };

        await using var presenceFactory = new GatewayTestFactory(new Dictionary<string, string>
        {
            ["presence-cluster"] = _chatStub.BaseUrl,
        });
        using var presenceClient = presenceFactory.CreateClient();

        var response = await presenceClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _chatStub.Requests.Should().Contain(r => r.Path == "/hubs/presence/negotiate");
    }
}
