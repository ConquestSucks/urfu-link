using System.Net;
using System.Net.Http.Json;
using UserService.Api.Domain;

namespace UserService.IntegrationTests;

public sealed class ServiceMetadataTests(UserServiceFactory factory) : IClassFixture<UserServiceFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RootEndpointShouldExposeServiceMetadata()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Contains("user-service", payload, StringComparison.Ordinal);
        Assert.Contains("postgresql", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadyHealthEndpointShouldReturnSuccess()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublishEndpointShouldRequireAuthentication()
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/integration/publish", UriKind.Relative),
            new { Name = "integration" });

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
