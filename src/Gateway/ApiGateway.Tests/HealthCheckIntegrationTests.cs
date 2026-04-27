using System.Net;
using ApiGateway.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests;

public sealed class HealthCheckIntegrationTests : IAsyncLifetime
{
    private StubDownstreamServer _userStub = null!;
    private StubDownstreamServer _chatStub = null!;
    private GatewayTestFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _userStub = await StubDownstreamServer.StartAsync();
        _chatStub = await StubDownstreamServer.StartAsync();

        _factory = new GatewayTestFactory(
            new Dictionary<string, string>
            {
                ["user-cluster"] = _userStub.BaseUrl,
                ["chat-cluster"] = _chatStub.BaseUrl,
                ["media-cluster"] = _userStub.BaseUrl,
                ["presence-cluster"] = _userStub.BaseUrl,
                ["notification-cluster"] = _userStub.BaseUrl,
                ["call-cluster"] = _userStub.BaseUrl,
                ["discipline-cluster"] = _userStub.BaseUrl,
            },
            extraConfiguration: new Dictionary<string, string?>
            {
                // Enable active checks at a high cadence so the test does not hang.
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Enabled"] = "true",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Interval"] = "00:00:01",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Timeout"] = "00:00:01",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Path"] = "/health/ready",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Policy"] = "ConsecutiveFailures",
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
    public async Task Live_AlwaysReturns200_RegardlessOfDownstream()
    {
        using var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ReturnsHealthy_WhenAllDownstreamReachable()
    {
        using var response = await _client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
