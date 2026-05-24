using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.Services.Presence.Infrastructure;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;
using Urfu.Link.Services.Presence.Messaging;
using Urfu.Link.Services.Presence.Realtime;
using Urfu.Link.Services.Presence.Services;

var builder = WebApplication.CreateBuilder(args);

// Migration mode: invoked by the Helm pre-upgrade Job (or the docker-compose
// `presence-migrations` sidecar in dev). When triggered, applies pending EF
// migrations and exits — no web host is started, no HostedServices run.
if (await MigrationCliRunner.TryRunMigrationsAsync<PresenceDbContext>(
    args,
    "PresenceService",
    services => services.AddDbContext<PresenceDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Primary"),
            npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "public")))))
{
    return;
}

builder.Services.AddGrpc();
builder.Services
    .AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.MaximumReceiveMessageSize = 64 * 1024;
    })
    .AddStackExchangeRedis(
        builder.Configuration["Infrastructure:Redis:Configuration"]
            ?? throw new InvalidOperationException("Infrastructure:Redis:Configuration is missing"),
        options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("urfu:signalr:presence");
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.ConnectRetry = 5;
        });
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "PresenceService API";
        s.Version = "v1";
    };
});

builder.Services.AddServiceDefaults(builder.Configuration, "presence-service");

// SignalR clients can't set the Authorization header during the WebSocket
// upgrade handshake — accept the bearer token via ?access_token= for /hubs/*.
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var existing = options.Events?.OnMessageReceived;
    options.Events ??= new JwtBearerEvents();
    options.Events.OnMessageReceived = async ctx =>
    {
        var path = ctx.HttpContext.Request.Path;
        var accessToken = ctx.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs", StringComparison.Ordinal))
        {
            ctx.Token = accessToken;
        }
        if (existing is not null)
        {
            await existing(ctx).ConfigureAwait(false);
        }
    };
});

builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddPresenceModule(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api/v1");
app.UseSwaggerGen();
app.MapScalarApiReference(o => o.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapGrpcService<InternalApiService>().RequireAuthorization(AuthenticationExtensions.InternalGrpcPolicy);
app.MapHub<PresenceHub>("/hubs/presence");

await app.RunAsync();

public partial class Program;
