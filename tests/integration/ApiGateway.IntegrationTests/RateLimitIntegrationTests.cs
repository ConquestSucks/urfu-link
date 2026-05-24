using System.Net;
using System.Net.Http.Headers;
using ApiGateway.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class RateLimitIntegrationTests : IAsyncLifetime
{
    private StubDownstreamServer _chatStub = null!;
    private StubDownstreamServer _mediaStub = null!;
    private GatewayTestFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _chatStub = await StubDownstreamServer.StartAsync();
        _mediaStub = await StubDownstreamServer.StartAsync();

        _factory = new GatewayTestFactory(new Dictionary<string, string>
        {
            ["chat-cluster"] = _chatStub.BaseUrl,
            ["media-cluster"] = _mediaStub.BaseUrl,
        });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _mediaStub.DisposeAsync();
        await _chatStub.DisposeAsync();
    }

    [Fact]
    public async Task ChatSend_Returns429_AfterPermitLimit()
    {
        const string sub = "user-rate-1";
        const int limit = 60;
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < limit + 5; i++)
        {
            using var request = BuildSendMessage(sub, conversationId: "conv-1");
            using var response = await _client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(limit).Should().AllSatisfy(s => s.Should().Be(HttpStatusCode.OK));
        statuses.Skip(limit).Should().Contain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ChatSend_RateLimit_PartitionedBySub()
    {
        const int limit = 60;

        for (var i = 0; i < limit; i++)
        {
            using var exhausting = BuildSendMessage("user-A", conversationId: "conv-1");
            using var _ = await _client.SendAsync(exhausting);
        }

        using var userARequest = BuildSendMessage("user-A", conversationId: "conv-1");
        using var userAOverflow = await _client.SendAsync(userARequest);

        userAOverflow.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "user-A has exhausted its bucket — proves the policy is enforced");

        using var userBRequest = BuildSendMessage("user-B", conversationId: "conv-1");
        using var userBResponse = await _client.SendAsync(userBRequest);

        userBResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "user-B has its own bucket — proves partitioning by Keycloak sub claim");
    }

    [Fact]
    public async Task MediaUploadInit_Returns429_AfterStricterLimit()
    {
        const string sub = "user-rate-media";
        const int limit = 30;
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < limit + 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/media/upload/init")
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sub);

            using var response = await _client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(limit).Should().AllSatisfy(s => s.Should().Be(HttpStatusCode.OK));
        statuses.Skip(limit).Should().Contain(HttpStatusCode.TooManyRequests);
    }

    private static HttpRequestMessage BuildSendMessage(string sub, string conversationId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/chat/conversations/{conversationId}/messages")
        {
            Content = new StringContent("{\"text\":\"hi\"}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sub);
        return request;
    }
}
