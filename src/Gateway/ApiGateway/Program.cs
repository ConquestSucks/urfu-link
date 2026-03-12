using System.Threading.RateLimiting;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPlatformJwtAuthentication(builder.Configuration)
    .AddPlatformObservability(builder.Configuration, "api-gateway");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientId = context.Request.Headers["X-Client-Id"].ToString();
        clientId = string.IsNullOrWhiteSpace(clientId) ? "anonymous" : clientId;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
            });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

app.Use((context, next) =>
{
    const string header = "X-Correlation-Id";
    if (!context.Request.Headers.TryGetValue(header, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
        context.Request.Headers[header] = correlationId;
    }

    context.Response.Headers[header] = correlationId;
    return next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "api-gateway",
    status = "ready",
    utc = DateTimeOffset.UtcNow,
}));
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapOpenApi();
app.MapReverseProxy();

app.Run();

public partial class Program;
