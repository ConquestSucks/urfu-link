using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Tests.Infrastructure;

/// <summary>
/// Real Kestrel-backed HTTP server used as a downstream stub in YARP integration tests.
/// Records every received request and lets the test assert on Method, Path, Headers, QueryString, Body.
/// Recorded requests and the optional response handler are stored in DI as singletons so the request
/// pipeline does not capture a closure on a partially-initialised <see cref="StubDownstreamServer"/>.
/// </summary>
public sealed class StubDownstreamServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly StubState _state;

    public string BaseUrl { get; }

    public IReadOnlyCollection<RecordedRequest> Requests => _state.Requests;

    public Func<HttpContext, RecordedRequest, Task>? ResponseHandler
    {
        get => _state.ResponseHandler;
        set => _state.ResponseHandler = value;
    }

    private StubDownstreamServer(WebApplication app, string baseUrl, StubState state)
    {
        _app = app;
        BaseUrl = baseUrl;
        _state = state;
    }

    public static async Task<StubDownstreamServer> StartAsync()
    {
        var state = new StubState();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(state);

        var app = builder.Build();
        app.Run(HandleRequestAsync);

        await app.StartAsync().ConfigureAwait(false);

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            ?? throw new InvalidOperationException("Stub downstream failed to expose any addresses.");

        return new StubDownstreamServer(app, addresses.First(), state);
    }

    private static async Task HandleRequestAsync(HttpContext ctx)
    {
        var state = ctx.RequestServices.GetRequiredService<StubState>();

        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms).ConfigureAwait(false);
        var body = Encoding.UTF8.GetString(ms.ToArray());
        var headers = ctx.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var recorded = new RecordedRequest(
            ctx.Request.Method,
            ctx.Request.Path.Value ?? string.Empty,
            ctx.Request.QueryString.Value ?? string.Empty,
            headers,
            body);

        state.Requests.Enqueue(recorded);

        var handler = state.ResponseHandler;
        if (handler is not null)
        {
            await handler(ctx, recorded).ConfigureAwait(false);
            return;
        }

        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "application/json";
        await ctx.Response
            .WriteAsync($"{{\"ok\":true,\"path\":\"{recorded.Path}\"}}")
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class StubState
    {
        public ConcurrentQueue<RecordedRequest> Requests { get; } = new();
        public Func<HttpContext, RecordedRequest, Task>? ResponseHandler { get; set; }
    }
}

public sealed record RecordedRequest(
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
