using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SmokeTests;

public sealed class UnitTest1(ApiGatewayFactory factory) : IClassFixture<ApiGatewayFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GatewayHealthEndpointsShouldReturnSuccess()
    {
        var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task GatewayShouldEmitCorrelationHeader()
    {
        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public void LocalKubernetesScriptsShouldUseKindAndHelm()
    {
        var upScript = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "scripts", "local-k8s-up.ps1")));

        Assert.Contains("kind create cluster", upScript, StringComparison.Ordinal);
        Assert.Contains("helm upgrade --install", upScript, StringComparison.Ordinal);
    }
}

public sealed class ApiGatewayFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
        });
    }
}
