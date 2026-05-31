using DotNet.Testcontainers.Images;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
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

    public async Task<DisciplineResponse> SeedDisciplineAsync(
        Guid ownerTeacherId,
        string? code = null,
        string title = "Intro to CS",
        string semester = "2026-spring")
    {
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDisciplineRepository>();
        var discipline = Discipline.CreateNew(
            code ?? $"CS-{Guid.NewGuid():N}"[..12],
            title,
            description: "Foundational course",
            semester,
            ownerTeacherId,
            coverAssetId: null,
            initiatedBy: ownerTeacherId);

        repository.Add(discipline);
        await repository.SaveChangesAsync(CancellationToken.None);
        return ToResponse(discipline);
    }

    public async Task<DisciplineResponse> SeedEnrollmentsAsync(
        Guid disciplineId,
        Guid enrolledBy,
        IReadOnlyList<(Guid UserId, DisciplineRole Role)> users)
    {
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDisciplineRepository>();
        var discipline = await repository.GetByIdAsync(disciplineId, CancellationToken.None)
            ?? throw new InvalidOperationException($"Discipline '{disciplineId}' was not found.");
        var defaultSubgroupId = discipline.Subgroups.First(s => !s.IsArchived).Id;

        foreach (var user in users)
        {
            discipline.Enroll(
                user.UserId,
                user.Role,
                user.Role == DisciplineRole.Student ? defaultSubgroupId : null,
                enrolledBy);
        }

        await repository.SaveChangesAsync(CancellationToken.None);
        return ToResponse(discipline);
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

    private static DisciplineResponse ToResponse(Discipline discipline)
        => new(
            discipline.Id,
            discipline.Code,
            discipline.Title,
            discipline.Description,
            discipline.Semester,
            discipline.OwnerTeacherId,
            discipline.CoverAssetId,
            discipline.CreatedAtUtc,
            discipline.UpdatedAtUtc,
            discipline.ArchivedAtUtc,
            discipline.Subgroups
                .Select(s => new DisciplineSubgroupResponse(
                    s.Id,
                    s.Name,
                    s.CreatedAtUtc,
                    s.UpdatedAtUtc,
                    s.ArchivedAtUtc))
                .ToList(),
            new DisciplinePermissionsResponse(
                CanUpdate: false,
                CanArchive: false,
                CanManageEnrollments: false,
                CanManageSubgroups: false),
            discipline.Enrollments
                .Select(e => new EnrollmentResponse(
                    e.UserId,
                    e.Role,
                    e.SubgroupId,
                    e.EnrolledAtUtc,
                    e.EnrolledBy))
                .ToList());

    private static void ReplaceAuthWithTestScheme(IServiceCollection services)
    {
        var authDescriptors = services
            .Where(d => d.ServiceType == typeof(IConfigureOptions<AuthenticationOptions>)
                || d.ServiceType == typeof(IPostConfigureOptions<AuthenticationOptions>)
                || d.ServiceType == typeof(IConfigureOptions<JwtBearerOptions>)
                || d.ServiceType == typeof(IPostConfigureOptions<JwtBearerOptions>)
                || d.ServiceType == typeof(IOptionsChangeTokenSource<JwtBearerOptions>))
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
