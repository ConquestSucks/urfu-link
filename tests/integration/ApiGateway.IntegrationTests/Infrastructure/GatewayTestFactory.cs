using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urfu.Link.BuildingBlocks.SessionRevocation;

namespace ApiGateway.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> wrapper that wires the API Gateway against
/// arbitrary downstream URLs, swaps JWT auth for <see cref="TestAuthHandler"/>, and stubs out Redis-backed
/// session revocation. Per-cluster active health checks are disabled by default so that probe traffic
/// does not interfere with assertions.
/// </summary>
public sealed class GatewayTestFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string> _clusterAddresses;
    private readonly IReadOnlyDictionary<string, string?>? _extraConfiguration;

    public GatewayTestFactory(
        IReadOnlyDictionary<string, string> clusterAddresses,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null)
    {
        _clusterAddresses = clusterAddresses;
        _extraConfiguration = extraConfiguration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://urfu-link.ghjc.ru",
                ["Auth:Authority"] = "http://localhost:9999/realms/test",
                ["Observability:Otlp:Endpoint"] = "http://localhost:9999",
                ["Infrastructure:Redis:Configuration"] = "localhost:6379",
            };

            foreach (var (cluster, url) in _clusterAddresses)
            {
                overrides[$"ReverseProxy:Clusters:{cluster}:Destinations:d1:Address"] = url;
                overrides[$"ReverseProxy:Clusters:{cluster}:HealthCheck:Active:Enabled"] = "false";
            }

            if (_extraConfiguration is not null)
            {
                foreach (var (key, value) in _extraConfiguration)
                {
                    overrides[key] = value;
                }
            }

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            services.RemoveAll<ISessionRevocationStore>();
            services.AddSingleton<ISessionRevocationStore, StubSessionRevocationStore>();
        });
    }
}
