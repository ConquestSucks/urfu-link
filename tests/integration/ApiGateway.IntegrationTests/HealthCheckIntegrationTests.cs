using System.Net;
using ApiGateway.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ApiGateway.IntegrationTests;

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

    [Fact]
    public async Task Ready_ReturnsServiceUnavailable_WhenAnyClusterHasNoUsableDestinations()
    {
        // Active YARP probes mark the destination Unhealthy after one failure round-trip.
        // We point user-cluster at an unreachable endpoint, then wait until a probe round runs.
        var unreachableFactory = new GatewayTestFactory(
            new Dictionary<string, string>
            {
                ["user-cluster"] = "http://127.0.0.1:1",
                ["chat-cluster"] = _chatStub.BaseUrl,
                ["media-cluster"] = _chatStub.BaseUrl,
                ["presence-cluster"] = _chatStub.BaseUrl,
                ["notification-cluster"] = _chatStub.BaseUrl,
                ["call-cluster"] = _chatStub.BaseUrl,
                ["discipline-cluster"] = _chatStub.BaseUrl,
            },
            extraConfiguration: new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Enabled"] = "true",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Interval"] = "00:00:01",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Timeout"] = "00:00:01",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Path"] = "/health/ready",
                ["ReverseProxy:Clusters:user-cluster:HealthCheck:Active:Policy"] = "ConsecutiveFailures",
            });
        await using var _ = unreachableFactory;
        using var unreachableClient = unreachableFactory.CreateClient();

        HttpResponseMessage? response = null;
        try
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                response?.Dispose();
                response = await unreachableClient.GetAsync("/health/ready");
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            response!.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
                "active probes mark unreachable user-cluster Unhealthy and the readiness check aggregates that into 503");
        }
        finally
        {
            response?.Dispose();
        }
    }
}
