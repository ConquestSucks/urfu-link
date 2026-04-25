using Amazon.S3;
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
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// Boots MediaService.Api with InMemory EF + mocked S3/Redis/auth. Suitable for
/// asserting endpoint contracts and the access-policy code-path without
/// requiring Docker. Full TestContainers (PG + MinIO) are tracked as a
/// follow-up — the seam is intentionally narrow so swapping in real
/// infrastructure is mostly registration changes here.
/// </summary>
public sealed class MediaServiceFactory : WebApplicationFactory<Program>
{
    public FakeOutboxWriter OutboxWriter { get; } = new();
    public IIdempotencyStore IdempotencyStore { get; } = Substitute.For<IIdempotencyStore>();
    public IMediaObjectStorage ObjectStorage { get; } = Substitute.For<IMediaObjectStorage>();
    public FakePresignedUrlGenerator UrlGenerator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:9999/realms/test",
                ["Observability:Otlp:Endpoint"] = "http://localhost:9999",
                ["ConnectionStrings:Primary"] = "test-placeholder",
                ["ConnectionStrings:Redis"] = "localhost:9999,abortConnect=false",
                ["Storage:Endpoint"] = "http://localhost:9999",
                ["Storage:AccessKey"] = "test",
                ["Storage:SecretKey"] = "test",
                ["Storage:PrivateBucket"] = "media-private",
                ["Storage:PublicBucket"] = "media-public",
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Outbox:ConnectionString"] = "test-placeholder",
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            ReplaceDbWithInMemory(services);

            // Idempotency / Redis
            IdempotencyStore.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(true));
            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton(IdempotencyStore);

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

            // Outbox / Kafka
            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter>(OutboxWriter);

            // Storage
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton(Substitute.For<IAmazonS3>());

            services.RemoveAll<IMediaObjectStorage>();
            services.AddSingleton(ObjectStorage);

            services.RemoveAll<IPresignedUrlGenerator>();
            services.AddSingleton<IPresignedUrlGenerator>(UrlGenerator);

            ReplaceAuthWithTestScheme(services);
        });
    }

    private static void ReplaceDbWithInMemory(IServiceCollection services)
    {
        var toRemove = services
            .Where(d => d.ServiceType.FullName?.Contains("MediaDbContext", StringComparison.Ordinal) == true
                || d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) == true
                || d.ServiceType.FullName?.Contains("IDbContextPool", StringComparison.Ordinal) == true
                || d.ServiceType.FullName?.Contains("IScopedDbContextLease", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var descriptor in toRemove)
        {
            services.Remove(descriptor);
        }

        var dbName = "TestDb_" + Guid.NewGuid().ToString("N");
        services.AddDbContext<MediaDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
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
