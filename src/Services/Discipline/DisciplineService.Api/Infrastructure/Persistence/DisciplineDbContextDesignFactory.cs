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
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=discipline_db;Username=postgres;Password=postgres";

    public DisciplineDbContext CreateDbContext(string[] args)
    {
        // Allow CI / Docker-based migration jobs to override the placeholder via the
        // DISCIPLINE_DESIGN_CONNECTION env var without editing this file. EF tooling
        // never executes runtime SQL against the connection string when generating
        // migrations — it just uses it to materialise the model — so the local
        // fallback is fine for `dotnet ef migrations add`.
        var fromEnv = Environment.GetEnvironmentVariable("DISCIPLINE_DESIGN_CONNECTION");
        var connectionString = string.IsNullOrWhiteSpace(fromEnv) ? DefaultConnectionString : fromEnv;
        var options = new DbContextOptionsBuilder<DisciplineDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new DisciplineDbContext(options);
    }
}
