using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DisciplineService.Api.Infrastructure.Persistence;

/// <summary>
/// Provides EF Core tooling (e.g. `dotnet ef migrations add`) with a DbContext
/// instance without spinning up the full Program.cs host pipeline. The connection
/// string is a placeholder — migrations are generated against the model only.
/// </summary>
internal sealed class DisciplineDbContextDesignFactory : IDesignTimeDbContextFactory<DisciplineDbContext>
{
    public DisciplineDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DisciplineDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=discipline_db;Username=postgres;Password=postgres")
            .Options;
        return new DisciplineDbContext(options);
    }
}
