using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MediaService.IntegrationTests.Infrastructure;

[Collection(IntegrationCollection.Name)]
public class GrpcAuthorizationTests
{
    private readonly MediaServiceFactory _factory;

    public GrpcAuthorizationTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void InternalApiGrpcEndpoints_RequireAuthorization()
    {
        // Force the host to build so endpoints are registered.
        _ = _factory.CreateClient();

        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var grpcEndpoints = dataSource.Endpoints
            .Where(e => e.DisplayName?.Contains("media.internal.v1.InternalApi", StringComparison.Ordinal) == true)
            .ToList();

        grpcEndpoints.Should().NotBeEmpty(
            "MediaService must expose the InternalApi gRPC service");

        foreach (var endpoint in grpcEndpoints)
        {
            endpoint.Metadata.GetMetadata<IAuthorizeData>()
                .Should().NotBeNull(
                    "every gRPC endpoint on InternalApi must enforce authentication; " +
                    $"endpoint '{endpoint.DisplayName}' is currently anonymous");
        }
    }
}
