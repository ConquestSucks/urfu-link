using DotNet.Testcontainers.Images;
using DisciplineService.Api.Infrastructure.Persistence;
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
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;

namespace DisciplineService.IntegrationTests.Infrastructure;

public sealed class DisciplineServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithImagePullPolicy(PullPolicy.Missing)
        .Build();

    public FakeOutboxWriter OutboxWriter { get; } = new();

    public InMemoryIdempotencyStore IdempotencyStore { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplyMigrationsAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public void ResetCapturedState()
    {
        OutboxWriter.Clear();
        IdempotencyStore.Clear();
        TestAuthHandler.CurrentPrincipal = null;
    }

    public async Task ResetDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DisciplineDbContext>();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE disciplines.enrollments, disciplines.disciplines, disciplines.outbox_messages RESTART IDENTITY CASCADE");
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
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Outbox:ConnectionString"] = "test-placeholder",
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            // Real idempotency semantics in tests so duplicate-key cases surface as 409.
            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton<IIdempotencyStore>(IdempotencyStore);

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter>(OutboxWriter);

            ReplaceAuthWithTestScheme(services);
        });
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<DisciplineDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var ctx = new DisciplineDbContext(options);
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
        services.AddAuthorization(options =>
            options.AddPolicy(
                AuthenticationExtensions.InternalGrpcPolicy,
                policy => policy
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()));
    }
}
