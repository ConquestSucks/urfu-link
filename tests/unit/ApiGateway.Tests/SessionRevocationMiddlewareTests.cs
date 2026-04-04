using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using Urfu.Link.Gateway.ApiGateway;

namespace ApiGateway.Tests;

public sealed class SessionRevocationMiddlewareTests
{
    private readonly ISessionRevocationStore _store = Substitute.For<ISessionRevocationStore>();
    private bool _nextCalled;

    private SessionRevocationMiddleware CreateMiddleware()
    {
        return new SessionRevocationMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        }, _store);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string sub, string sid)
    {
        var claims = new[]
        {
            new Claim("sub", sub),
            new Claim("sid", sid),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        return context;
    }

    private static DefaultHttpContext CreateAnonymousContext()
    {
        return new DefaultHttpContext();
    }

    [Fact]
    public async Task ShouldPassThroughWhenUnauthenticated()
    {
        var middleware = CreateMiddleware();
        var context = CreateAnonymousContext();

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task ShouldPassThroughWhenNotRevoked()
    {
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("user-1", "session-1");
        _store.IsRevokedAsync("user-1", "session-1", Arg.Any<CancellationToken>())
            .Returns(false);

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturn401WhenSessionRevoked()
    {
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("user-1", "session-1");
        _store.IsRevokedAsync("user-1", "session-1", Arg.Any<CancellationToken>())
            .Returns(true);

        await middleware.InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ShouldSetRevocationHeaderWhenRevoked()
    {
        var middleware = CreateMiddleware();
        var context = CreateAuthenticatedContext("user-1", "session-1");
        _store.IsRevokedAsync("user-1", "session-1", Arg.Any<CancellationToken>())
            .Returns(true);

        await middleware.InvokeAsync(context);

        Assert.Equal("true", context.Response.Headers["X-Session-Revoked"].ToString());
    }

    [Fact]
    public async Task ShouldPassThroughWhenNoSubClaim()
    {
        var middleware = CreateMiddleware();
        var identity = new ClaimsIdentity([new Claim("sid", "session-1")], "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task ShouldPassThroughWhenNoSidClaim()
    {
        var middleware = CreateMiddleware();
        var identity = new ClaimsIdentity([new Claim("sub", "user-1")], "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        await middleware.InvokeAsync(context);

        Assert.True(_nextCalled);
    }
}
