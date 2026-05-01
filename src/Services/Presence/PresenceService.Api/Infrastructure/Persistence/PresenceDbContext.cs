using Microsoft.EntityFrameworkCore;
using Urfu.Link.Services.Presence.Domain.Aggregates;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence;

public sealed class PresenceDbContext(DbContextOptions<PresenceDbContext> options) : DbContext(options)
{
    public const string Schema = "presence";

    public DbSet<LastSeen> LastSeens => Set<LastSeen>();

    public DbSet<LastSeenHistoryEntry> LastSeenHistory => Set<LastSeenHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PresenceDbContext).Assembly);
    }
}
