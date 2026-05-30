using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.Services.Call.Application;
using Urfu.Link.Services.Call.Domain;
using Urfu.Link.Services.Call.Endpoints;
using Urfu.Link.Services.Call.Infrastructure;
using Urfu.Link.Services.Call.Messaging;
using Urfu.Link.Services.Call.Realtime;
using Urfu.Link.Services.Call.Services;
using Urfu.Link.Services.Call.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services
    .AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.MaximumReceiveMessageSize = 64 * 1024;
    })
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddStackExchangeRedis(
        builder.Configuration["Infrastructure:Redis:Configuration"]
            ?? throw new InvalidOperationException("Infrastructure:Redis:Configuration is missing"),
        options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("urfu:signalr:calls");
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.ConnectRetry = 5;
        });
builder.Services.AddSingleton<IUserIdProvider, CallUserIdProvider>();
builder.Services.AddServiceDefaults(builder.Configuration, "call-service");

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
builder.Services.AddHostedService<CallRingTimeoutWorker>();
builder.Services.AddCallModule();

var app = builder.Build();

app.MapServiceDefaults();
app.MapGrpcService<InternalApiService>();
app.MapHub<CallHub>("/hubs/calls");
app.MapCallEndpoints();

app.MapGet("/", (ServiceProfile descriptor) => Results.Ok(new
{
    service = descriptor.ServiceName,
    datastore = descriptor.Datastore,
    utc = DateTimeOffset.UtcNow,
}));

app.MapPost("/api/v1/integration/publish", async (
    PublishSampleEventRequest request,
    SampleEventDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    var messageId = await dispatcher.PublishAsync(request, cancellationToken).ConfigureAwait(false);
    return Results.Accepted($"/api/v1/integration/messages/{messageId}", new { MessageId = messageId });
})
.RequireAuthorization()
.AddEndpointFilter<IdempotencyEndpointFilter>();

app.Run();

public partial class Program;
