using System.Diagnostics;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Observability;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using Urfu.Link.Gateway.ApiGateway;
using Urfu.Link.Gateway.ApiGateway.HealthChecks;
using Urfu.Link.Gateway.ApiGateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPlatformJwtAuthentication(builder.Configuration)
    .AddPlatformObservability(builder.Configuration, "api-gateway")
    .AddSessionRevocation(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(["application/json", "application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey =
            context.User.FindFirst("sub")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 200,
            TokensPerPeriod = 200,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });

    options.AddPolicy("chat-send", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy("media-upload", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy("search", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpClient();

builder.Services
    .AddHealthChecks()
    .AddCheck<YarpDestinationsHealthCheck>(
        name: "yarp-destinations",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

app.UseResponseCompression();

app.UseMiddleware<SecurityHeadersMiddleware>();

// Required for SignalR WebSocket upgrades to be proxied by YARP. Must be enabled before MapReverseProxy.
app.UseWebSockets();

app.UseCors();

app.Use((context, next) =>
{
    const string header = "X-Correlation-Id";
    if (!context.Request.Headers.TryGetValue(header, out var correlationId)
        || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
        context.Request.Headers[header] = correlationId;
    }

    context.Response.Headers[header] = correlationId;
    Activity.Current?.SetTag("correlation.id", correlationId.ToString());
    return next();
});

app.UseAuthentication();
app.UseAuthorization();

// Rate limiter must come after authentication so that policies can partition by Keycloak `sub`.
app.UseRateLimiter();

app.UseMiddleware<SessionRevocationMiddleware>();

app.MapGet("/", () => Results.Ok(new
{
    service = "api-gateway",
    status = "ready",
    utc = DateTimeOffset.UtcNow,
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapOpenApi();
app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
