using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
        var context = CreateContext(method, "/api/users/me", out var responseFeature);
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

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
        var context = CreateContext(method, "/api/users/me", out var responseFeature);
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("no-store", context.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task ShouldUseNoStoreWhenSafeResponseSetsCookie()
    {
        var context = CreateContext("GET", "/api/users/me", out var responseFeature);
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers.Append(HeaderNames.SetCookie, "session=abc; HttpOnly");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        Assert.Equal("no-store", context.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task ShouldNotSetCacheControlForHubRoutes()
    {
        var context = CreateContext("GET", "/hubs/chat", out var responseFeature);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    private static SecurityHeadersMiddleware CreateMiddleware(RequestDelegate next) => new(next);

    private static DefaultHttpContext CreateContext(
        string method,
        string path,
        out StartingResponseFeature responseFeature)
    {
        responseFeature = new StartingResponseFeature
        {
            Body = new MemoryStream(),
        };

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(responseFeature);

        var context = new DefaultHttpContext(features);
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private sealed class StartingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];
        private readonly List<(Func<object, Task> Callback, object State)> _onCompleted = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _onStarting.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _onCompleted.Add((callback, state));
        }

        public async Task FireOnStartingAsync()
        {
            if (HasStarted)
            {
                return;
            }

            for (var i = _onStarting.Count - 1; i >= 0; i--)
            {
                var (callback, state) = _onStarting[i];
                await callback(state);
            }

            HasStarted = true;
        }
    }
}
