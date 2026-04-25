using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Presence.Infrastructure.Persistence;

namespace PresenceService.IntegrationTests.Infrastructure;

/// <summary>
/// Boots PresenceService.Api against a real PostgreSQL container. Background
/// workers (Kafka consumer, outbox publisher, sweeper added later) are stripped;
/// Redis stays mocked at this stage and switches to a real container in step 6.
/// Migrations are applied directly against the container before the host is built,
/// reproducing the Helm pre-install --migrate Job.
/// </summary>
public sealed class PresenceServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7.4.2-alpine")
        .Build();

    public FakeOutboxWriter OutboxWriter { get; } = new();
    public IIdempotencyStore IdempotencyStore { get; } = Substitute.For<IIdempotencyStore>();

    public string RedisConnectionString => $"{_redis.GetConnectionString()},allowAdmin=true";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        await ApplyMigrationsAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public void ResetCapturedState()
    {
        OutboxWriter.Clear();
        TestAuthHandler.CurrentPrincipal = null;
    }

    public async Task ResetDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PresenceDbContext>();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE presence.last_seen RESTART IDENTITY CASCADE");

        var multiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var endpoints = multiplexer.GetEndPoints();
        if (endpoints.Length > 0)
        {
            var server = multiplexer.GetServer(endpoints[0]);
            await server.FlushDatabaseAsync();
        }

        ResetCapturedState();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:9999/realms/test",
                ["Observability:Otlp:Endpoint"] = "http://localhost:9999",
                ["ConnectionStrings:Primary"] = _postgres.GetConnectionString(),
                ["Infrastructure:Redis:Configuration"] = RedisConnectionString,
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Outbox:RedisConfiguration"] = RedisConnectionString,
                ["Idempotency:RedisConfiguration"] = RedisConnectionString,
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            IdempotencyStore.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(true));
            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton(IdempotencyStore);

            // Real Redis container backs IConnectionMultiplexer (registered by AddOutbox).
            // We replace the outbox writer with a fake to capture published events.
            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter>(OutboxWriter);

            ReplaceAuthWithTestScheme(services);
        });
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<PresenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var ctx = new PresenceDbContext(options);
        await ctx.Database.MigrateAsync();
    }

    private static void ReplaceAuthWithTestScheme(IServiceCollection services)
    {
        var authDescriptors = services
            .Where(d => d.ServiceType.FullName?.Contains("AuthenticationScheme", StringComparison.Ordinal) == true
                || d.ServiceType.FullName?.Contains("JwtBearer", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in authDescriptors)
        {
            services.Remove(d);
        }

        services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
    }
}
