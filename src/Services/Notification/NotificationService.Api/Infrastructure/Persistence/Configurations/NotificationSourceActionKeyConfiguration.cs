using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationSourceActionKeyConfiguration : IEntityTypeConfiguration<NotificationSourceActionKey>
{
    public void Configure(EntityTypeBuilder<NotificationSourceActionKey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("notification_source_action_keys");

        builder.HasKey(k => new { k.RecipientUserId, k.SourceActionId });

        builder.Property(k => k.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(k => k.SourceActionId)
            .HasColumnName("source_action_id")
            .HasMaxLength(NotificationAggregate.SourceActionIdMaxLength)
            .IsRequired();
        builder.Property(k => k.NotificationId).HasColumnName("notification_id");
        builder.Property(k => k.NotificationCreatedAtUtc).HasColumnName("notification_created_at_utc");
        builder.Property(k => k.Priority)
            .HasColumnName("priority")
            .HasConversion<short>();
        builder.Property(k => k.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(k => k.NotificationId)
            .HasDatabaseName("ix_notification_source_action_keys_notification");
    }
}
