using Amazon.S3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Idempotency;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Persistence;

namespace UserService.Tests.Infrastructure;

public sealed class UserServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
                ["Storage:BucketName"] = "test-avatars",
                ["Keycloak:AdminUrl"] = "http://localhost:9999",
                ["Keycloak:Realm"] = "test",
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Outbox:ConnectionString"] = "test-placeholder",
            });
        });

        builder.ConfigureServices(services =>
        {
            ReplaceDbWithInMemory(services);
            ReplaceExternalServicesWithMocks(services);
            ReplaceAuthWithTestScheme(services);
        });
    }

    private static void ReplaceDbWithInMemory(IServiceCollection services)
    {
        // Remove all EF descriptors that reference UserDbContext (pool, lease, options, context itself)
        var toRemove = services
            .Where(d =>
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GenericTypeArguments.Contains(typeof(UserDbContext))) ||
                d.ServiceType == typeof(DbContextOptions<UserDbContext>) ||
                d.ServiceType == typeof(UserDbContext))
            .ToList();
        foreach (var descriptor in toRemove)
            services.Remove(descriptor);

        services.AddDbContext<UserDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
    }

    private static void ReplaceExternalServicesWithMocks(IServiceCollection services)
    {
        Replace<IAvatarStorage>(services, Substitute.For<IAvatarStorage>());
        Replace<ISessionManager>(services, Substitute.For<ISessionManager>());
        Replace<IAmazonS3>(services, Substitute.For<IAmazonS3>());
        Replace<IConnectionMultiplexer>(services, Substitute.For<IConnectionMultiplexer>());
        Replace<IIdempotencyStore>(services, Substitute.For<IIdempotencyStore>());
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        var existing = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in existing) services.Remove(d);
        services.AddSingleton(instance);
    }

    private static void ReplaceAuthWithTestScheme(IServiceCollection services)
    {
        var authDescriptors = services
            .Where(d => d.ServiceType.FullName?.Contains("AuthenticationScheme", StringComparison.Ordinal) == true ||
                        d.ServiceType.FullName?.Contains("JwtBearer", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in authDescriptors) services.Remove(d);

        services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
    }
}
