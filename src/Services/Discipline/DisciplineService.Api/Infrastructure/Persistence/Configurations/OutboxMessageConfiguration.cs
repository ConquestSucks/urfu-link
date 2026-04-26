using DisciplineService.Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisciplineService.Api.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.Topic).HasColumnName("topic").HasMaxLength(128).IsRequired();
        builder.Property(m => m.Key).HasColumnName("message_key").HasMaxLength(128).IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(m => m.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(m => m.Attempts).HasColumnName("attempts");
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(1024);
        builder.Property(m => m.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");

        // Worker reads `WHERE published_at_utc IS NULL ORDER BY occurred_at_utc LIMIT N` —
        // a partial index on the unpublished slice keeps the relay query cheap as the
        // table grows with every committed event.
        builder.HasIndex(m => m.OccurredAtUtc)
            .HasDatabaseName("ix_outbox_messages_unpublished_occurred_at")
            .HasFilter("published_at_utc IS NULL");
    }
}
