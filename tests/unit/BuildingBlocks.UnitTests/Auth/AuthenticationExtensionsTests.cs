using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Auth;

namespace Urfu.Link.BuildingBlocks.UnitTests.Auth;

public sealed class AuthenticationExtensionsTests
{
    [Fact]
    public async Task InternalGrpc_policy_uses_default_jwt_scheme_when_internal_auth_is_not_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddPlatformJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(AuthenticationExtensions.InternalGrpcPolicy);
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        policy.Should().NotBeNull();
        policy!.AuthenticationSchemes.Should().ContainSingle()
            .Which.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        (await schemes.GetSchemeAsync(AuthenticationExtensions.InternalJwtBearerScheme))
            .Should().BeNull();
    }

    [Fact]
    public async Task InternalGrpc_policy_uses_internal_jwt_scheme_when_internal_auth_is_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TokenHeader"] = "X-Pomerium-Jwt-Assertion",
                ["Auth:JwksUrl"] = "http://pomerium/.well-known/pomerium/jwks.json",
                ["Auth:Audience"] = "urfu-link.ghjc.ru",
                ["Auth:ValidIssuer"] = "urfu-link.ghjc.ru",
                ["Auth:Internal:JwksUrl"] = "http://keycloak/realms/urfu-link/protocol/openid-connect/certs",
                ["Auth:Internal:Audience"] = "urfu-link-api",
                ["Auth:Internal:ValidIssuer"] = "https://id.ghjc.ru/realms/urfu-link",
                ["Auth:Internal:RoleClaim"] = "groups",
            })
            .Build();

        services.AddPlatformJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(AuthenticationExtensions.InternalGrpcPolicy);
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        policy.Should().NotBeNull();
        policy!.AuthenticationSchemes.Should().ContainSingle()
            .Which.Should().Be(AuthenticationExtensions.InternalJwtBearerScheme);
        (await schemes.GetSchemeAsync(AuthenticationExtensions.InternalJwtBearerScheme))
            .Should().NotBeNull();
    }
}
