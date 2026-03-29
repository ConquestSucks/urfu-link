using System.Net;

namespace SmokeTests;

public sealed class ApiGatewayTests(ApiGatewayFactory factory) : IClassFixture<ApiGatewayFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GatewayHealthEndpointsShouldReturnSuccess()
    {
        var live = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));
        var ready = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task GatewayShouldEmitCorrelationHeader()
    {
        var response = await _client.GetAsync(new Uri("/", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public void LocalKubernetesScriptsShouldUseKindAndHelm()
    {
        var upScript = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "scripts", "local-k8s-up.ps1")));

        Assert.Contains("kind create cluster", upScript, StringComparison.Ordinal);
        Assert.Contains("helm upgrade --install", upScript, StringComparison.Ordinal);
    }
}
