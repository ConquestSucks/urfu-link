using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Aggregates;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("deliveries");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.NotificationId).HasColumnName("notification_id");
        builder.Property(d => d.Channel).HasColumnName("channel").HasConversion<short>();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(d => d.Address).HasColumnName("address").HasMaxLength(Delivery.AddressMaxLength).IsRequired();
        builder.Property(d => d.Provider).HasColumnName("provider").HasConversion<short?>();
        builder.Property(d => d.PushDeviceId).HasColumnName("push_device_id");
        builder.Property(d => d.Attempts).HasColumnName("attempts");
        builder.Property(d => d.LastError).HasColumnName("last_error").HasMaxLength(Delivery.ErrorMaxLength);
        builder.Property(d => d.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc");
        builder.Property(d => d.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(d => d.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(d => d.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(Delivery.ProviderMessageIdMaxLength);
        builder.Property(d => d.SkipReason).HasColumnName("skip_reason").HasMaxLength(Delivery.ErrorMaxLength);

        builder.HasIndex(d => new { d.Channel, d.Status, d.NextAttemptAtUtc })
            .HasDatabaseName("ix_deliveries_channel_status_next_attempt");

        builder.HasIndex(d => d.NotificationId)
            .HasDatabaseName("ix_deliveries_notification");
    }
}
