using Amazon.S3;
using Amazon.S3.Model;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Persistence;
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
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// Boots MediaService.Api against real PostgreSQL and MinIO containers.
/// External hosted services (Kafka outbox, retention/cleanup workers) are
/// stripped, and Redis / IIdempotencyStore stay mocked because they are
/// not exercised by the endpoint contracts under test. Migrations are
/// applied automatically by Program.cs on host start.
/// </summary>
public sealed class MediaServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string MinioRootUser = "minioadmin";
    private const string MinioRootPassword = "minioadmin";
    private const string PrivateBucket = "media-private";
    private const string PublicBucket = "media-public";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:RELEASE.2025-09-07T16-13-09Z")
        .WithUsername(MinioRootUser)
        .WithPassword(MinioRootPassword)
        .Build();

    public FakeOutboxWriter OutboxWriter { get; } = new();
    public IIdempotencyStore IdempotencyStore { get; } = Substitute.For<IIdempotencyStore>();

    public string MinioEndpoint => $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());
        await CreateBucketsAsync();
        await ApplyMigrationsAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _minio.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Clears any captured state that survives across tests: outbox events
    /// and the static auth principal. Cheap; call between tests inside the
    /// same class.
    /// </summary>
    public void ResetCapturedState()
    {
        OutboxWriter.Clear();
        TestAuthHandler.CurrentPrincipal = null;
    }

    /// <summary>
    /// Wipes the media schema so a fresh test class starts with an empty DB.
    /// Cascades through asset → session / grant FKs and resets identity
    /// generators in one round-trip. Bucket contents in MinIO are left in
    /// place because object keys are GUID-prefixed and never collide.
    /// </summary>
    public async Task ResetDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE media.media_assets, media.upload_sessions, media.media_access_grants RESTART IDENTITY CASCADE");
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
                ["ConnectionStrings:Redis"] = "localhost:9999,abortConnect=false",
                ["Storage:Endpoint"] = MinioEndpoint,
                ["Storage:AccessKey"] = MinioRootUser,
                ["Storage:SecretKey"] = MinioRootPassword,
                ["Storage:PrivateBucket"] = PrivateBucket,
                ["Storage:PublicBucket"] = PublicBucket,
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Outbox:ConnectionString"] = "test-placeholder",
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Strip Kafka consumer / outbox dispatcher / workers — none of
            // them are exercised by REST contract tests and they only add
            // background noise that races with assertions.
            services.RemoveAll<IHostedService>();

            // Idempotency / Redis: keep mocked. Real Redis is not in the
            // service-under-test path for these endpoints.
            IdempotencyStore.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(true));
            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton(IdempotencyStore);

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

            // Outbox: keep the in-memory fake so we can assert published events
            // without spinning up Kafka.
            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter>(OutboxWriter);

            ReplaceAuthWithTestScheme(services);
        });
    }

    private async Task CreateBucketsAsync()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = MinioEndpoint,
            ForcePathStyle = true,
        };
        using var s3 = new AmazonS3Client(MinioRootUser, MinioRootPassword, config);

        foreach (var bucket in new[] { PrivateBucket, PublicBucket })
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        }
    }

    private async Task ApplyMigrationsAsync()
    {
        // Production now applies migrations via the dedicated --migrate CLI flag
        // (Helm pre-install/pre-upgrade Job). Tests reproduce that by running the
        // same EF migration set directly against the container before the host
        // is built.
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var ctx = new MediaDbContext(options);
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
