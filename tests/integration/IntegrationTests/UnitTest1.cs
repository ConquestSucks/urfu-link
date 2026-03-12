using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Urfu.Link.Services.User.Domain;

namespace IntegrationTests;

public sealed class UnitTest1(UserServiceFactory factory) : IClassFixture<UserServiceFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task RootEndpointShouldExposeServiceMetadata()
    {
        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Contains("user-service", payload, StringComparison.Ordinal);
        Assert.Contains("postgresql", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadyHealthEndpointShouldReturnSuccess()
    {
        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublishEndpointShouldRequireAuthentication()
    {
        var response = await client.PostAsJsonAsync(new Uri("/api/v1/integration/publish", UriKind.Relative), new { Name = "integration" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ServiceProfileShouldDescribeBoundedContext()
    {
        var descriptor = new ServiceProfile("user-service", "postgresql", "urfu.user.events.v1", "user.sample.v1");

        Assert.Equal("user-service", descriptor.ServiceName);
        Assert.Equal("postgresql", descriptor.Datastore);
    }
}

public sealed class UserServiceFactory : WebApplicationFactory<Program>
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

