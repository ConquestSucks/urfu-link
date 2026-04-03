using System.Net;
using System.Net.Http.Json;
using UserService.Api.Application.Contracts.Responses;
using UserService.IntegrationTests.Helpers;

namespace UserService.IntegrationTests;

public sealed class DeviceEndpointTests(UserServiceFactory factory) : IClassFixture<UserServiceFactory>
{
    // Minimal unsigned JWT with sid claim: {"alg":"none"}.{"sid":"test-pomerium-sid"}.
    private const string TestPomeriumJwt =
        "eyJhbGciOiJub25lIn0.eyJzaWQiOiJ0ZXN0LXBvbWVyaXVtLXNpZCJ9.";

    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Real-Ip", "127.0.0.1");
        client.DefaultRequestHeaders.Add("X-Pomerium-Jwt-Assertion", TestPomeriumJwt);
        return client;
    }

    [Fact]
    public async Task GetDevicesShouldReturnSessionList()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(new Uri("/api/v1/me/devices", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessions = await response.Content.ReadFromJsonAsync<List<DeviceSessionResponse>>();
        Assert.NotNull(sessions);
        Assert.NotEmpty(sessions);
        Assert.Contains(sessions, s => s.IsCurrent);
    }

    [Fact]
    public async Task TerminateDeviceShouldReturn204()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync(
            new Uri("/api/v1/me/devices/test-session-002", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TerminateAllDevicesShouldReturn204()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync(
            new Uri("/api/v1/me/devices", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
