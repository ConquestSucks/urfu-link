using System.Diagnostics;
using System.IO.Compression;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Model;
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
        .Concat(["application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(ResolvePartitionKey(context), _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 200,
            TokensPerPeriod = 200,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    options.AddPolicy("chat-send", context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolvePartitionKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    options.AddPolicy("media-upload", context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolvePartitionKey(context), _ => new FixedWindowRateLimiterOptions
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

builder.Services.AddSingleton<YarpClusterStateRegistry>();
builder.Services.AddSingleton<IClusterChangeListener>(sp =>
    sp.GetRequiredService<YarpClusterStateRegistry>());
builder.Services.AddSingleton<YarpDestinationsHealthCheck>();

builder.Services
    .AddHealthChecks()
    .Add(new HealthCheckRegistration(
        name: "yarp-destinations",
        factory: sp => sp.GetRequiredService<YarpDestinationsHealthCheck>(),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]));

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

// Defence in depth for SignalR hub routes: gateway does not validate the JWT (downstream does), but
// it requires the access_token query parameter to be present so anonymous traffic is rejected here.
app.UseMiddleware<HubAccessTokenPresenceMiddleware>();

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

var readinessStatusCodes = new Dictionary<HealthStatus, int>
{
    [HealthStatus.Healthy] = StatusCodes.Status200OK,
    [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
};

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes = readinessStatusCodes,
});

app.MapOpenApi();
app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);

static string ResolvePartitionKey(HttpContext context) =>
    context.User.FindFirstValue("sub")
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "anonymous";

public partial class Program;
