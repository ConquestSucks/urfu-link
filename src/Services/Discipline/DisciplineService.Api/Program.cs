using DisciplineService.Api.Infrastructure;
using DisciplineService.Api.Infrastructure.Auth;
using DisciplineService.Api.Infrastructure.Persistence;
using DisciplineService.Api.Messaging;
using DisciplineService.Api.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Migration mode: invoked by the Helm pre-upgrade Job (or the docker-compose
// `discipline-migrations` sidecar in dev). When triggered, applies pending EF
// migrations and exits — no web host is started, no HostedServices run.
if (await MigrationCliRunner.TryRunMigrationsAsync<DisciplineDbContext>(
    args,
    "DisciplineService",
    services => services.AddDbContext<DisciplineDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Primary"),
            npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "public")))))
{
    return;
}

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

// Internal gRPC API is only callable by callers that carry either the dedicated
// service:discipline-read realm role (granted to chat-service-internal in
// Keycloak) or the global admin role. End-user JWTs without one of these roles
// land on 403 even after authentication, so an enrolled student cannot fetch
// another discipline's roster through gRPC.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(InternalGrpcAuthorizationPolicy.PolicyName, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(InternalGrpcAuthorizationPolicy.AllowedRoles));

// Transactional outbox lives in AddDisciplineModule (EfOutboxWriter +
// DisciplineOutboxRelay). Only the Kafka producer ships from BuildingBlocks
// here — we never use the Redis-based outbox in this service because every
// event has a corresponding domain row and must commit atomically with it.
builder.Services.AddKafkaPublisher(builder.Configuration);
builder.Services.AddHostedService<KafkaConsumerWorker>();
builder.Services.AddDisciplineModule(builder.Configuration);

var app = builder.Build();

// Schema is owned by the Helm pre-upgrade migrations Job (or the dev sidecar)
// and applied via `dotnet DisciplineService.Api.dll --migrate` exactly once
// per release. Auto-migrating in startup raced replicas on the schema upgrade
// at boot — moved out deliberately.
app.MapServiceDefaults();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
    c.Serializer.Options.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});
app.UseSwaggerGen();
app.MapScalarApiReference(o =>
    o.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapGrpcService<InternalApiService>()
    .RequireAuthorization(
        AuthenticationExtensions.InternalGrpcPolicy,
        InternalGrpcAuthorizationPolicy.PolicyName);

await app.RunAsync();

public partial class Program;
