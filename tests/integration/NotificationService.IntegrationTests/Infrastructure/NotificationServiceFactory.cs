using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Realtime;

namespace NotificationService.IntegrationTests.Infrastructure;

public sealed class NotificationServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17.9-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7.4.2-alpine")
        .Build();

    /// <summary>
    /// Mutable presence stub injected in place of the real <see cref="IPresenceClient"/>
    /// so individual tests can flip the user's web state and assert the resulting
    /// routing decision (e.g. push must be skipped for chat categories when online on web).
    /// </summary>
    public TestPresenceClient PresenceClient { get; } = new();

    /// <summary>
    /// Test stub for <see cref="IUserPreferencesClient"/> so tests don't depend on a real
    /// UserService gRPC endpoint. Defaults to <see cref="UserPreferences.Default"/> per user.
    /// </summary>
    public TestUserPreferencesClient PreferencesClient { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync()).ConfigureAwait(false);
        await ApplyMigrationsAsync().ConfigureAwait(false);
        await EnsurePartitionForCurrentMonthAsync().ConfigureAwait(false);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask()).ConfigureAwait(false);
    }

    public async Task ResetDataAsync()
    {
        PresenceClient.OnlineOnWeb = false;
        await using var scope = Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE notifications.notifications, notifications.deliveries, notifications.push_devices, notifications.outbox_messages RESTART IDENTITY CASCADE")
            .ConfigureAwait(false);

        var multiplexer = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var endpoint = multiplexer.GetEndPoints().FirstOrDefault();
        if (endpoint is not null)
        {
            var server = multiplexer.GetServer(endpoint);
            var redisDb = multiplexer.GetDatabase();
            var keys = new List<RedisKey>();
            await foreach (var key in server.KeysAsync(pattern: "urfu:*").ConfigureAwait(false))
            {
                keys.Add(key);
            }

            if (keys.Count > 0)
            {
                await redisDb.KeyDeleteAsync([.. keys]).ConfigureAwait(false);
            }
        }

        TestAuthHandler.CurrentPrincipal = null;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:9999/realms/test",
                ["Auth:ValidIssuer"] = "http://localhost:9999/realms/test",
                ["Auth:Audience"] = "test",
                ["Observability:Otlp:Endpoint"] = "http://localhost:9999",
                ["ConnectionStrings:Primary"] = _postgres.GetConnectionString(),
                ["Infrastructure:Redis:Configuration"] = _redis.GetConnectionString(),
                ["Idempotency:RedisConfiguration"] = _redis.GetConnectionString(),
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Notification:Push:Provider"] = "fake",
                ["Notification:Smtp:Provider"] = "fake",
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Strip background workers — tests drive the pipeline directly via HTTP/gRPC.
            services.RemoveAll<IHostedService>();

            // Outbox publisher needs Kafka — replace with a no-op since we don't run Kafka.
            services.RemoveAll<IKafkaPublisher>();
            services.AddSingleton<IKafkaPublisher, NoopKafkaPublisher>();

            // Replace whichever IPresenceClient was wired (real GrpcPresenceClient when
            // PresenceService:GrpcEndpoint is set, OfflinePresenceClient otherwise) with
            // a controllable fake so each test owns the recipient's web state.
            services.RemoveAll<IPresenceClient>();
            services.AddSingleton<IPresenceClient>(PresenceClient);

            // Same for user preferences — tests don't run a UserService gRPC server.
            services.RemoveAll<IUserPreferencesClient>();
            services.AddSingleton<IUserPreferencesClient>(PreferencesClient);

            // Replace the SignalR-backed broadcaster with a no-op: tests don't run
            // WebSocket clients and the real broadcaster's Redis backplane is wired
            // before the test factory rewrites configuration.
            services.RemoveAll<INotificationBroadcaster>();
            services.AddSingleton<INotificationBroadcaster, NoopNotificationBroadcaster>();

            ReplaceAuthWithTestScheme(services);
        });
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var ctx = new NotificationDbContext(options);
        await ctx.Database.MigrateAsync().ConfigureAwait(false);
    }

    private async Task EnsurePartitionForCurrentMonthAsync()
    {
        // The initial migration seeds 3 months ahead of `DateTime.UtcNow`; this keeps
        // tests robust if they run near a month boundary.
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var ctx = new NotificationDbContext(options);
        var manager = new PartitionManager(ctx);
        await manager.EnsureAsync(YearMonth.FromUtc(DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);
    }

    private static void ReplaceAuthWithTestScheme(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IAuthenticationHandlerProvider) ||
                descriptor.ServiceType == typeof(IAuthenticationService) ||
                descriptor.ServiceType == typeof(IAuthorizationHandlerProvider))
            {
                services.RemoveAt(i);
            }
        }

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, _ => { });
        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        services.AddAuthorization(o => o.DefaultPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
            .RequireAuthenticatedUser()
            .Build());
    }
}

internal sealed class NoopKafkaPublisher : IKafkaPublisher
{
    public Task PublishAsync<TEvent>(string topic, Urfu.Link.BuildingBlocks.Contracts.Integration.IntegrationEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
        where TEvent : Urfu.Link.BuildingBlocks.Contracts.Integration.IIntegrationEvent
        => Task.CompletedTask;

    public Task PublishSerializedAsync(string topic, string key, string payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
