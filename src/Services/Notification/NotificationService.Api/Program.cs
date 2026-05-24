using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Infrastructure;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Messaging;
using Urfu.Link.Services.Notification.Realtime;
using Urfu.Link.Services.Notification.Services;

var builder = WebApplication.CreateBuilder(args);

// Migration mode: invoked by the Helm pre-upgrade Job (or the docker-compose `*-migrations`
// sidecar in dev). When triggered, applies pending EF migrations and exits — no web host
// is started, no HostedServices run. Normal app startup must NEVER auto-migrate, otherwise
// every replica races on the schema upgrade at boot.
if (await MigrationCliRunner.TryRunMigrationsAsync<NotificationDbContext>(
    args,
    "NotificationService",
    services => services.AddDbContext<NotificationDbContext>(options =>
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
            options.Configuration.ChannelPrefix = RedisChannel.Literal("urfu:signalr:notifications");
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.ConnectRetry = 5;
        });
builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
builder.Services.AddFastEndpoints();
builder.Services.AddServiceDefaults(builder.Configuration, "notification-service");

// SignalR clients can't set the Authorization header during the WebSocket upgrade handshake —
// accept the bearer token via ?access_token= for /hubs/* paths.
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
builder.Services.AddNotificationModule(builder.Configuration);
builder.Services.AddHostedService<ChatEventsConsumer>();
builder.Services.AddHostedService<DisciplineEventsConsumer>();
builder.Services.AddHostedService<CallEventsConsumer>();
builder.Services.AddHostedService<UserEventsConsumer>();
builder.Services.AddHostedService<SystemEventsConsumer>();
builder.Services.AddHostedService<Urfu.Link.Services.Notification.Workers.PushDispatcherWorker>();
builder.Services.AddHostedService<Urfu.Link.Services.Notification.Workers.EmailDispatcherWorker>();
builder.Services.AddHostedService<Urfu.Link.Services.Notification.Infrastructure.Outbox.NotificationOutboxRelay>();
builder.Services.AddHostedService<Urfu.Link.Services.Notification.Workers.RetentionCleanupWorker>();

var app = builder.Build();

app.MapServiceDefaults();
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api/v1");
app.MapGrpcService<InternalApiService>();
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/", (ServiceProfile descriptor) => Results.Ok(new
{
    service = descriptor.ServiceName,
    datastore = descriptor.Datastore,
    utc = DateTimeOffset.UtcNow,
}));

await app.RunAsync();

public partial class Program;
