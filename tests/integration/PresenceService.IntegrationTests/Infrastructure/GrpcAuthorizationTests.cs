using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.Services.Presence.Infrastructure.Auth;

namespace PresenceService.IntegrationTests.Infrastructure;

[Collection(IntegrationCollection.Name)]
public class GrpcAuthorizationTests
{
    private readonly PresenceServiceFactory _factory;

    public GrpcAuthorizationTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void InternalApiGrpcEndpoints_UseInternalGrpcPolicy()
    {
        _ = _factory.CreateClient();

        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var grpcEndpoints = dataSource.Endpoints
            .Where(e => e.DisplayName?.Contains("presence.internal.v1.InternalApi", StringComparison.Ordinal) == true)
            .ToList();

        grpcEndpoints.Should().NotBeEmpty();
        foreach (var endpoint in grpcEndpoints)
        {
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .ToList();

            policies.Should().Contain(AuthenticationExtensions.InternalGrpcPolicy);
            policies.Should().Contain(InternalGrpcAuthorizationPolicy.PolicyName);
        }

        var options = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var internalPolicy = options.GetPolicy(AuthenticationExtensions.InternalGrpcPolicy);
        internalPolicy.Should().NotBeNull();
        internalPolicy!.AuthenticationSchemes.Should().Contain(TestAuthHandler.SchemeName);

        var rolePolicy = options.GetPolicy(InternalGrpcAuthorizationPolicy.PolicyName);
        rolePolicy.Should().NotBeNull();
        rolePolicy!.Requirements.OfType<RolesAuthorizationRequirement>()
            .Should().ContainSingle()
            .Which.AllowedRoles.Should()
            .Contain(InternalGrpcAuthorizationPolicy.PresenceInternalRole);
    }
}
