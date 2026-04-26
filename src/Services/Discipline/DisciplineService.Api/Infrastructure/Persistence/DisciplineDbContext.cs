using DisciplineService.Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace DisciplineService.Api.Infrastructure.Persistence;

public sealed class DisciplineDbContext(DbContextOptions<DisciplineDbContext> options) : DbContext(options)
{
    public DbSet<Discipline> Disciplines => Set<Discipline>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("disciplines");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DisciplineDbContext).Assembly);
    }
}
