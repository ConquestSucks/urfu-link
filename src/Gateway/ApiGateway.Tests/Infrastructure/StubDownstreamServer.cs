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
/// </summary>
public sealed class StubDownstreamServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string BaseUrl { get; }

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public Func<HttpContext, RecordedRequest, Task>? ResponseHandler { get; set; }

    private StubDownstreamServer(WebApplication app, string baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public static async Task<StubDownstreamServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();

        StubDownstreamServer? instance = null;

        app.Run(async ctx =>
        {
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

            instance!.Requests.Enqueue(recorded);

            if (instance.ResponseHandler is not null)
            {
                await instance.ResponseHandler(ctx, recorded).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            await ctx.Response
                .WriteAsync($"{{\"ok\":true,\"path\":\"{recorded.Path}\"}}")
                .ConfigureAwait(false);
        });

        await app.StartAsync().ConfigureAwait(false);

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            ?? throw new InvalidOperationException("Stub downstream failed to expose any addresses.");

        var url = addresses.First();
        instance = new StubDownstreamServer(app, url);
        return instance;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed record RecordedRequest(
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
