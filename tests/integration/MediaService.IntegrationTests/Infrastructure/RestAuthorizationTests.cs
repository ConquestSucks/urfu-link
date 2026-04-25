using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MediaService.IntegrationTests.Infrastructure;

[Collection(IntegrationCollection.Name)]
public class RestAuthorizationTests
{
    private readonly MediaServiceFactory _factory;

    public RestAuthorizationTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AllRestEndpoints_RequireAuthorization()
    {
        _ = _factory.CreateClient();

        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var mediaEndpoints = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw
                && raw.Contains("media", StringComparison.OrdinalIgnoreCase)
                && !raw.Contains("InternalApi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        mediaEndpoints.Should().NotBeEmpty(
            "MediaService must register REST endpoints under the /media route group");

        foreach (var endpoint in mediaEndpoints)
        {
            endpoint.Metadata.GetMetadata<IAuthorizeData>()
                .Should().NotBeNull(
                    "every REST endpoint under /media must enforce authorization through MediaGroup; " +
                    $"endpoint '{endpoint.RoutePattern.RawText}' is currently anonymous");
        }
    }
}
