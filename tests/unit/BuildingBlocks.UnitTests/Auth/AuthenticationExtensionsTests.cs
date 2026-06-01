using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
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
        (await schemes.GetSchemeAsync(AuthenticationExtensions.EdgeJwtBearerScheme))
            .Should().BeNull();
        (await schemes.GetSchemeAsync(AuthenticationExtensions.MobileJwtBearerScheme))
            .Should().BeNull();
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

    [Fact]
    public async Task Mobile_auth_registers_edge_and_mobile_schemes_when_mobile_auth_is_configured()
    {
        var services = new ServiceCollection();
        var configuration = BuildDualAuthConfiguration();

        services.AddPlatformJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var policyOptions = provider.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        (await schemes.GetSchemeAsync(AuthenticationExtensions.EdgeJwtBearerScheme))
            .Should().NotBeNull();
        (await schemes.GetSchemeAsync(AuthenticationExtensions.MobileJwtBearerScheme))
            .Should().NotBeNull();

        SelectScheme(policyOptions, context =>
            {
                context.Request.Headers.Authorization = "Bearer mobile-token";
            })
            .Should().Be(AuthenticationExtensions.MobileJwtBearerScheme);

        SelectScheme(policyOptions, context =>
            {
                context.Request.Headers.Authorization = "bearer mobile-token";
            })
            .Should().Be(AuthenticationExtensions.MobileJwtBearerScheme);

        SelectScheme(policyOptions, context =>
            {
                context.Request.Headers["X-Pomerium-Jwt-Assertion"] = "signed-pomerium-jwt";
            })
            .Should().Be(AuthenticationExtensions.EdgeJwtBearerScheme);

        SelectScheme(policyOptions, context =>
            {
                context.Request.Headers["X-Pomerium-Jwt-Assertion"] = "";
                context.Request.Headers.Authorization = "Bearer mobile-token";
            })
            .Should().Be(AuthenticationExtensions.MobileJwtBearerScheme);

        SelectScheme(policyOptions, context =>
            {
                context.Request.Path = "/hubs/chat";
                context.Request.QueryString = new QueryString("?access_token=mobile-token");
            })
            .Should().Be(AuthenticationExtensions.MobileJwtBearerScheme);
    }

    [Fact]
    public async Task Mobile_auth_configures_keycloak_token_validation_without_changing_edge_validation()
    {
        var services = new ServiceCollection();
        var configuration = BuildDualAuthConfiguration();

        services.AddPlatformJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        var edge = jwtOptions.Get(AuthenticationExtensions.EdgeJwtBearerScheme);
        edge.Audience.Should().Be("urfu-link.ghjc.ru");
        edge.TokenValidationParameters.ValidIssuer.Should().Be("urfu-link.ghjc.ru");
        edge.TokenValidationParameters.RoleClaimType.Should().Be("groups");

        var mobile = jwtOptions.Get(AuthenticationExtensions.MobileJwtBearerScheme);
        mobile.Audience.Should().Be("urfu-link-api");
        mobile.TokenValidationParameters.ValidIssuer.Should().Be("https://id.ghjc.ru/realms/urfu-link");
        mobile.TokenValidationParameters.RoleClaimType.Should().Be("roles");

        await VerifyHubQueryTokenIsAcceptedByMobileScheme(mobile);
    }

    private static IConfiguration BuildDualAuthConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TokenHeader"] = "X-Pomerium-Jwt-Assertion",
                ["Auth:JwksUrl"] = "http://pomerium/.well-known/pomerium/jwks.json",
                ["Auth:Audience"] = "urfu-link.ghjc.ru",
                ["Auth:ValidIssuer"] = "urfu-link.ghjc.ru",
                ["Auth:RoleClaim"] = "groups",
                ["Auth:Mobile:JwksUrl"] = "http://keycloak/realms/urfu-link/protocol/openid-connect/certs",
                ["Auth:Mobile:Audience"] = "urfu-link-api",
                ["Auth:Mobile:ValidIssuer"] = "https://id.ghjc.ru/realms/urfu-link",
                ["Auth:Mobile:RoleClaim"] = "roles",
            })
            .Build();

    private static string? SelectScheme(
        PolicySchemeOptions policyOptions,
        Action<DefaultHttpContext> arrange)
    {
        var context = new DefaultHttpContext();
        arrange(context);

        return policyOptions.ForwardDefaultSelector?.Invoke(context);
    }

    private static async Task VerifyHubQueryTokenIsAcceptedByMobileScheme(JwtBearerOptions mobile)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/hubs/presence";
        context.Request.QueryString = new QueryString("?access_token=mobile-token");
        var scheme = new AuthenticationScheme(
            AuthenticationExtensions.MobileJwtBearerScheme,
            AuthenticationExtensions.MobileJwtBearerScheme,
            typeof(IAuthenticationHandler));
        var messageContext = new MessageReceivedContext(context, scheme, mobile);

        await mobile.Events.MessageReceived(messageContext);

        messageContext.Token.Should().Be("mobile-token");
    }
}
