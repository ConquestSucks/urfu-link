using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Aggregates;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.Topic).HasColumnName("topic").HasMaxLength(128).IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(m => m.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(m => m.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(m => m.Attempts).HasColumnName("attempts");
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(1024);

        builder.HasIndex(m => new { m.PublishedAtUtc, m.NextAttemptAtUtc })
            .HasDatabaseName("ix_outbox_pending");
    }
}
