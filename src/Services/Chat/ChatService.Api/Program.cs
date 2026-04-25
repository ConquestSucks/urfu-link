using FastEndpoints;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddFastEndpoints();
builder.Services.AddServiceDefaults(builder.Configuration, "chat-service");
builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddChatModule(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api/v1");
app.MapGrpcService<InternalApiService>();

app.MapGet("/", (ServiceProfile descriptor) => Results.Ok(new
{
    service = descriptor.ServiceName,
    datastore = descriptor.Datastore,
    utc = DateTimeOffset.UtcNow,
}));

app.Run();

public partial class Program;
