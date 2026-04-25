using Microsoft.EntityFrameworkCore;
using Urfu.Link.Services.Presence.Domain.Aggregates;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence;

public sealed class PresenceDbContext(DbContextOptions<PresenceDbContext> options) : DbContext(options)
{
    public DbSet<LastSeen> LastSeens => Set<LastSeen>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("presence");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PresenceDbContext).Assembly);
    }
}
