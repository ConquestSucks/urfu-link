using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Presence.Domain.Aggregates;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="LastSeenHistoryEntry"/>. The parent table is created
/// as <c>PARTITION BY RANGE (recorded_at_utc)</c> in the migration; EF only sees the
/// logical schema. Monthly partitions are managed by <c>PartitionManager</c>.
/// </summary>
public sealed class LastSeenHistoryConfiguration : IEntityTypeConfiguration<LastSeenHistoryEntry>
{
    public void Configure(EntityTypeBuilder<LastSeenHistoryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("last_seen_history");

        // Composite key (id, recorded_at_utc) — Postgres requires the partition key column
        // to be part of the primary key on a partitioned table.
        builder.HasKey(h => new { h.Id, h.RecordedAtUtc });
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(h => h.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(h => h.LastPlatform).HasColumnName("last_platform").HasConversion<int>().IsRequired();
        builder.Property(h => h.RecordedAtUtc).HasColumnName("recorded_at_utc").IsRequired();

        builder.HasIndex(h => h.UserId).HasDatabaseName("ix_last_seen_history_user_id");
    }
}
