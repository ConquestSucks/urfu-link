using System.Net;
using System.Net.Http.Headers;
using ApiGateway.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class RoutingIntegrationTests : IAsyncLifetime
{
    private StubDownstreamServer _userStub = null!;
    private StubDownstreamServer _chatStub = null!;
    private StubDownstreamServer _mediaStub = null!;
    private StubDownstreamServer _presenceStub = null!;
    private StubDownstreamServer _notificationStub = null!;
    private StubDownstreamServer _callStub = null!;
    private StubDownstreamServer _disciplineStub = null!;
    private GatewayTestFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _userStub = await StubDownstreamServer.StartAsync();
        _chatStub = await StubDownstreamServer.StartAsync();
        _mediaStub = await StubDownstreamServer.StartAsync();
        _presenceStub = await StubDownstreamServer.StartAsync();
        _notificationStub = await StubDownstreamServer.StartAsync();
        _callStub = await StubDownstreamServer.StartAsync();
        _disciplineStub = await StubDownstreamServer.StartAsync();

        _factory = new GatewayTestFactory(new Dictionary<string, string>
        {
            ["user-cluster"] = _userStub.BaseUrl,
            ["chat-cluster"] = _chatStub.BaseUrl,
            ["media-cluster"] = _mediaStub.BaseUrl,
            ["presence-cluster"] = _presenceStub.BaseUrl,
            ["notification-cluster"] = _notificationStub.BaseUrl,
            ["call-cluster"] = _callStub.BaseUrl,
            ["discipline-cluster"] = _disciplineStub.BaseUrl,
        });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _disciplineStub.DisposeAsync();
        await _callStub.DisposeAsync();
        await _notificationStub.DisposeAsync();
        await _presenceStub.DisposeAsync();
        await _mediaStub.DisposeAsync();
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

    public static TheoryData<HttpMethod, string, string, string> RestRouteCases => new()
    {
        { HttpMethod.Get, "/api/users/me", "user-cluster", "/api/v1/me" },
        { HttpMethod.Get, "/api/users/search?q=nik", "user-cluster", "/api/v1/search" },
        { HttpMethod.Post, "/api/media/upload/init", "media-cluster", "/api/v1/media/upload/init" },
        { HttpMethod.Post, "/api/media/upload/complete", "media-cluster", "/api/v1/media/upload/complete" },
        { HttpMethod.Get, "/api/media/asset-1/download-url", "media-cluster", "/api/v1/media/asset-1/download-url" },
        { HttpMethod.Get, "/api/media/asset-1/metadata", "media-cluster", "/api/v1/media/asset-1/metadata" },
        { HttpMethod.Delete, "/api/media/asset-1", "media-cluster", "/api/v1/media/asset-1" },
        { HttpMethod.Get, "/api/chat/conversations", "chat-cluster", "/api/v1/chat/conversations" },
        { HttpMethod.Get, "/api/chat/conversations/conversation-1/pinned", "chat-cluster", "/api/v1/chat/conversations/conversation-1/pinned" },
        { HttpMethod.Post, "/api/chat/conversations/conversation-1/messages", "chat-cluster", "/api/v1/chat/conversations/conversation-1/messages" },
        { HttpMethod.Get, "/api/presence/users/user-1", "presence-cluster", "/api/v1/presence/users/user-1" },
        { HttpMethod.Post, "/api/presence/users/batch", "presence-cluster", "/api/v1/presence/users/batch" },
        { HttpMethod.Post, "/api/presence/sessions/device-1/disconnect", "presence-cluster", "/api/v1/presence/sessions/device-1/disconnect" },
        { HttpMethod.Get, "/api/notifications/badge", "notification-cluster", "/api/v1/badge" },
        { HttpMethod.Post, "/api/calls/rooms", "call-cluster", "/api/v1/rooms" },
        { HttpMethod.Get, "/api/disciplines", "discipline-cluster", "/api/v1/disciplines" },
    };

    [Theory]
    [MemberData(nameof(RestRouteCases))]
    public async Task RestRoutes_ProxyPublicApiPrefixes_ToVersionedDownstreamPaths(
        HttpMethod method,
        string publicPath,
        string clusterId,
        string expectedDownstreamPath)
    {
        using var request = new HttpRequestMessage(method, publicPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-42");
        if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = GetStub(clusterId);
        downstream.Requests.Should().ContainSingle();
        var recorded = downstream.Requests.Single();
        recorded.Path.Should().Be(expectedDownstreamPath);
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
    public async Task HubRoute_WithConfiguredTokenHeader_ProxiesToDownstream()
    {
        await using var pomeriumFactory = new GatewayTestFactory(
            new Dictionary<string, string>
            {
                ["chat-cluster"] = _chatStub.BaseUrl,
            },
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Auth:TokenHeader"] = "X-Pomerium-Jwt-Assertion",
            });
        using var pomeriumClient = pomeriumFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/chat/negotiate")
        {
            Content = new StringContent(string.Empty),
        };
        request.Headers.Add("X-Pomerium-Jwt-Assertion", "signed-pomerium-jwt");

        var response = await pomeriumClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _chatStub.Requests.Should().ContainSingle(r => r.Path == "/hubs/chat/negotiate")
            .Which.Headers.Should().ContainKey("X-Pomerium-Jwt-Assertion")
            .WhoseValue.Should().Be("signed-pomerium-jwt");
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
            ["presence-cluster"] = _presenceStub.BaseUrl,
        });
        using var presenceClient = presenceFactory.CreateClient();

        var response = await presenceClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _presenceStub.Requests.Should().Contain(r => r.Path == "/hubs/presence/negotiate");
    }

    private StubDownstreamServer GetStub(string clusterId) => clusterId switch
    {
        "user-cluster" => _userStub,
        "chat-cluster" => _chatStub,
        "media-cluster" => _mediaStub,
        "presence-cluster" => _presenceStub,
        "notification-cluster" => _notificationStub,
        "call-cluster" => _callStub,
        "discipline-cluster" => _disciplineStub,
        _ => throw new ArgumentOutOfRangeException(nameof(clusterId), clusterId, null),
    };
}
