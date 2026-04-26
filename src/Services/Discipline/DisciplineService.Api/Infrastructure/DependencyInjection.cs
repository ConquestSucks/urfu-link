using DisciplineService.Api.Domain;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;

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

        return services;
    }
}
