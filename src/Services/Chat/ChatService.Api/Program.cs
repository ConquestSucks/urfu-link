using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;
using Urfu.Link.Services.Chat.Services;

var builder = WebApplication.CreateBuilder(args);

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
    .AddJsonProtocol(options =>
    {
        // Соответствует FastEndpoints-сериализатору: enum'ы отдаём строками,
        // чтобы клиент мог типизировать ConversationType как union "Direct" | "Group".
        options.PayloadSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddStackExchangeRedis(
        builder.Configuration["Infrastructure:Redis:Configuration"]
            ?? throw new InvalidOperationException("Infrastructure:Redis:Configuration is missing"),
        options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("urfu:signalr:chat");
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.ConnectRetry = 5;
        });
builder.Services.AddSingleton<IUserIdProvider, ChatUserIdProvider>();
builder.Services.AddFastEndpoints(o =>
{
    o.Assemblies = [typeof(Program).Assembly];
    o.DisableAutoDiscovery = true;
});
builder.Services.AddServiceDefaults(builder.Configuration, "chat-service");

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
builder.Services.AddChatModule(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
    // Enum'ы сериализуем как строки ("Direct"/"Group" вместо 0/1) — клиент типизирует
    // ConversationType / ParticipantRole / AttachmentType строковыми union-ами.
    c.Serializer.Options.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});
app.MapGrpcService<InternalApiService>().RequireAuthorization(AuthenticationExtensions.InternalGrpcPolicy);
app.MapHub<ChatHub>("/hubs/chat");

app.MapGet("/", (ServiceProfile descriptor) => Results.Ok(new
{
    service = descriptor.ServiceName,
    datastore = descriptor.Datastore,
    utc = DateTimeOffset.UtcNow,
}));

app.Run();

public partial class Program;
