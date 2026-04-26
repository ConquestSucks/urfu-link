using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Infrastructure;
using Urfu.Link.Services.Notification.Messaging;
using Urfu.Link.Services.Notification.Realtime;
using Urfu.Link.Services.Notification.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSignalR();
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
builder.Services.AddHostedService<Urfu.Link.Services.Notification.Workers.PushDispatcherWorker>();

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

app.Run();

public partial class Program;
