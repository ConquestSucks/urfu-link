using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.Services.Notification.Application;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Infrastructure;
using Urfu.Link.Services.Notification.Messaging;
using Urfu.Link.Services.Notification.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddServiceDefaults(builder.Configuration, "notification-service");
builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddNotificationModule();

var app = builder.Build();

app.MapServiceDefaults();
app.MapGrpcService<InternalApiService>();

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

