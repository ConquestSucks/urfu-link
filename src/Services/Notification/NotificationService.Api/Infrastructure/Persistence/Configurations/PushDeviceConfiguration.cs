using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Aggregates;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("push_devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.UserId).HasColumnName("user_id");
        builder.Property(d => d.Provider).HasColumnName("provider").HasConversion<short>();
        builder.Property(d => d.Token).HasColumnName("token").HasMaxLength(PushDevice.TokenMaxLength).IsRequired();
        builder.Property(d => d.DeviceFingerprint).HasColumnName("device_fingerprint").HasMaxLength(PushDevice.FingerprintMaxLength).IsRequired();
        builder.Property(d => d.Platform).HasColumnName("platform").HasMaxLength(PushDevice.PlatformMaxLength).IsRequired();
        builder.Property(d => d.AppVersion).HasColumnName("app_version").HasMaxLength(PushDevice.AppVersionMaxLength);
        builder.Property(d => d.Locale).HasColumnName("locale").HasMaxLength(PushDevice.LocaleMaxLength).IsRequired();
        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(d => d.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(d => d.IsActive).HasColumnName("is_active");
        builder.Property(d => d.DeactivationReason).HasColumnName("deactivation_reason").HasMaxLength(PushDevice.ReasonMaxLength);
        builder.Property(d => d.DeactivatedAtUtc).HasColumnName("deactivated_at_utc");

        builder.HasIndex(d => new { d.UserId, d.Provider, d.Token })
            .IsUnique()
            .HasDatabaseName("ux_push_devices_user_provider_token");

        builder.HasIndex(d => new { d.UserId, d.IsActive })
            .HasDatabaseName("ix_push_devices_user_active");
    }
}
