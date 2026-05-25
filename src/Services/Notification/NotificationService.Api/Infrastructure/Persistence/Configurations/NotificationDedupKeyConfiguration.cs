using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationDedupKeyConfiguration : IEntityTypeConfiguration<NotificationDedupKey>
{
    public void Configure(EntityTypeBuilder<NotificationDedupKey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("notification_dedup_keys");

        builder.HasKey(k => new { k.RecipientUserId, k.SourceEventId, k.NotificationType });

        builder.Property(k => k.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(k => k.SourceEventId).HasColumnName("source_event_id");
        builder.Property(k => k.NotificationType)
            .HasColumnName("notification_type")
            .HasMaxLength(NotificationAggregate.NotificationTypeMaxLength)
            .IsRequired();
        builder.Property(k => k.NotificationId).HasColumnName("notification_id");
        builder.Property(k => k.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(k => k.NotificationId)
            .HasDatabaseName("ix_notification_dedup_keys_notification");
    }
}
