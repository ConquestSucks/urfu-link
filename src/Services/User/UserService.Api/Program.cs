using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using UserService.Api.Application.Contracts;
using UserService.Api.Infrastructure;
using UserService.Api.Infrastructure.OpenApi;
using UserService.Api.Infrastructure.Persistence;
using UserService.Api.Messaging;
using UserService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "UserService API";
        s.Version = "v1";
        s.SchemaSettings.SchemaProcessors.Add(new OptionalNSwagSchemaProcessor());
    };
});
builder.Services.AddServiceDefaults(builder.Configuration, "user-service");

// DataProtection keys are persisted to Redis to survive pod restarts and support scaling.
// IConnectionMultiplexer is already registered by AddIdempotency (via AddServiceDefaults).
// IConfigureOptions<KeyManagementOptions> resolves it lazily from the final DI container —
// WebApplicationFactory.ConfigureServices overrides are applied before the host starts.
builder.Services.AddDataProtection().SetApplicationName("urfu-link-user-service");
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
    new ConfigureOptions<KeyManagementOptions>(opts =>
        opts.XmlRepository = new RedisXmlRepository(
            () => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
            "urfu:dp:user-service")));

builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddUserModule(builder.Configuration);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}

app.MapServiceDefaults();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
    c.Serializer.Options.Converters.Add(new OptionalJsonConverterFactory());
});
app.UseSwaggerGen();
// Point Scalar at FastEndpoints/NSwag — avoids JsonSchemaExporter crashing on Optional<T>.
app.MapScalarApiReference(o =>
    o.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapGrpcService<InternalApiService>();

await app.RunAsync();

public partial class Program;
