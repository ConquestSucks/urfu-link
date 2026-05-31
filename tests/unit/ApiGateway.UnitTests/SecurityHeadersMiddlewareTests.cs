using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Urfu.Link.Gateway.ApiGateway.Middleware;

namespace ApiGateway.UnitTests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task ShouldUseRevalidationCacheHeaderForSuccessfulSafeApiResponses(string method)
    {
        var context = CreateContext(method, "/api/users/me");
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return ctx.Response.StartAsync();
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "private, max-age=0, must-revalidate",
            context.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Theory]
    [InlineData("POST", StatusCodes.Status200OK)]
    [InlineData("GET", StatusCodes.Status401Unauthorized)]
    [InlineData("GET", StatusCodes.Status500InternalServerError)]
    public async Task ShouldUseNoStoreForUnsafeOrUnsuccessfulResponses(string method, int statusCode)
    {
        var context = CreateContext(method, "/api/users/me");
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            return ctx.Response.StartAsync();
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("no-store", context.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task ShouldUseNoStoreWhenSafeResponseSetsCookie()
    {
        var context = CreateContext("GET", "/api/users/me");
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers.Append(HeaderNames.SetCookie, "session=abc; HttpOnly");
            return ctx.Response.StartAsync();
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("no-store", context.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task ShouldNotSetCacheControlForHubRoutes()
    {
        var context = CreateContext("GET", "/hubs/chat");
        var middleware = CreateMiddleware(ctx => ctx.Response.StartAsync());

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    private static SecurityHeadersMiddleware CreateMiddleware(RequestDelegate next) => new(next);

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
