using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;
using Urfu.Link.Services.Presence.Infrastructure;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;
using Urfu.Link.Services.Presence.Messaging;
using Urfu.Link.Services.Presence.Services;

if (args.Any(a => string.Equals(a, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        var migrateBuilder = WebApplication.CreateBuilder(args);
        migrateBuilder.Services.AddDbContextPool<PresenceDbContext>(options =>
            options.UseNpgsql(migrateBuilder.Configuration.GetConnectionString("Primary")));
        var migrateApp = migrateBuilder.Build();
        await using var migrateScope = migrateApp.Services.CreateAsyncScope();
        var migrateDb = migrateScope.ServiceProvider.GetRequiredService<PresenceDbContext>();
        await migrateDb.Database.MigrateAsync();
        await Console.Out.WriteLineAsync("PresenceService migrations applied successfully.");
        return;
    }
#pragma warning disable CA1031 // Migration entry-point must catch any failure to surface it as a non-zero exit code for Helm.
    catch (Exception ex)
#pragma warning restore CA1031
    {
        await Console.Error.WriteLineAsync($"PresenceService migration failed: {ex.GetType().Name}: {ex.Message}");
        await Console.Error.WriteLineAsync(ex.ToString());
        Environment.ExitCode = 1;
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
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
builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddPresenceModule(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api/v1");
app.UseSwaggerGen();
app.MapScalarApiReference(o => o.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapGrpcService<InternalApiService>().RequireAuthorization();

await app.RunAsync();

public partial class Program;
