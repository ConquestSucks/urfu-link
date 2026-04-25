using FastEndpoints;
using FastEndpoints.Swagger;
using MediaService.Api.Infrastructure;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.Api.Messaging;
using MediaService.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "MediaService API";
        s.Version = "v1";
    };
});

builder.Services.AddServiceDefaults(builder.Configuration, "media-service");

// DataProtection keys persisted to Redis (so they survive pod restarts and scale-out).
builder.Services.AddDataProtection().SetApplicationName("urfu-link-media-service");
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
    new ConfigureOptions<KeyManagementOptions>(opts =>
        opts.XmlRepository = new RedisXmlRepository(
            () => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
            "urfu:dp:media-service")));

builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddMediaModule(builder.Configuration);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

app.MapServiceDefaults();
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api/v1");
app.UseSwaggerGen();
app.MapScalarApiReference(o =>
    o.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapGrpcService<InternalApiService>().RequireAuthorization();

await app.RunAsync();

public partial class Program;
