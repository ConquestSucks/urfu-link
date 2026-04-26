using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Outbox;
using DisciplineService.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace DisciplineService.Api.Infrastructure;

public static class ModuleRegistration
{
    public static IServiceCollection AddDisciplineModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new ServiceProfile(
            "discipline-service",
            "postgresql",
            KafkaTopicNames.DisciplineEvents,
            "discipline.created.v1"));

        services.AddDbContextPool<DisciplineDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Primary")));

        services.AddScoped<IDisciplineRepository, DisciplineRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddSingleton<DisciplineAuthorizationService>();

        // Transactional outbox: events stage through the scoped DbContext and commit
        // alongside the aggregate change. The relay worker drains the table and
        // produces to Kafka with SKIP LOCKED so multiple replicas stay coordinated.
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.AddOptions<DisciplineOutboxRelayOptions>()
            .Bind(configuration.GetSection(DisciplineOutboxRelayOptions.SectionName));
        services.AddHostedService<DisciplineOutboxRelay>();

        return services;
    }
}
