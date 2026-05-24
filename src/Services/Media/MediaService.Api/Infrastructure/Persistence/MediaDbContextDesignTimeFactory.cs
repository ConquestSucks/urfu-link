using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediaService.Api.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef migrations add</c> at design time. The connection string
/// is intentionally a placeholder — migrations only need the model, not a live DB.
/// </summary>
internal sealed class MediaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=media_design_time;Username=postgres;Password=postgres")
            .Options;
        return new MediaDbContext(options);
    }
}
