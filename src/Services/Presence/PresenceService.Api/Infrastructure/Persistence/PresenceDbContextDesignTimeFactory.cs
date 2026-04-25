using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence;

internal sealed class PresenceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PresenceDbContext>
{
    public PresenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PresenceDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=presence_design_time;Username=postgres;Password=postgres")
            .Options;
        return new PresenceDbContext(options);
    }
}
