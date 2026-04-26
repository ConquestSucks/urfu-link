using DisciplineService.Api.Domain;
using DisciplineService.Api.Messaging;
using DisciplineService.Api.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Scalar.AspNetCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "DisciplineService API";
        s.Version = "v1";
    };
});
builder.Services.AddServiceDefaults(builder.Configuration, "discipline-service");

// ServiceProfile is consumed by Outbox/Repository to label outgoing events.
// Will be moved to AddDisciplineModule once Stage 4 lands.
builder.Services.AddSingleton(new ServiceProfile(
    "discipline-service",
    "postgresql",
    KafkaTopicNames.DisciplineEvents,
    "discipline.created.v1"));

builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();

var app = builder.Build();

app.MapServiceDefaults();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
});
app.UseSwaggerGen();
app.MapScalarApiReference(o =>
    o.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapGrpcService<InternalApiService>();

await app.RunAsync();

public partial class Program;
