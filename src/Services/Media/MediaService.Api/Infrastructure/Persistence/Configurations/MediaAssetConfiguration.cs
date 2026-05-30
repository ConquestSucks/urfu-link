using MediaService.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaService.Api.Infrastructure.Persistence.Configurations;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("media_assets");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(a => a.Visibility).HasColumnName("visibility").HasConversion<int>().IsRequired();
        builder.Property(a => a.Kind).HasColumnName("kind").HasConversion<int>().IsRequired();
        builder.Property(a => a.Bucket).HasColumnName("bucket").HasMaxLength(64).IsRequired();
        builder.Property(a => a.ObjectKey).HasColumnName("object_key").HasMaxLength(512).IsRequired();
        builder.Property(a => a.Size).HasColumnName("size_bytes").IsRequired();
        builder.Property(a => a.MimeType).HasColumnName("mime_type").HasMaxLength(127).IsRequired();
        builder.Property(a => a.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(a => a.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(a => a.Checksum).HasColumnName("checksum").HasMaxLength(128);
        builder.Property(a => a.State).HasColumnName("state").HasConversion<int>().IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(a => a.UploadedAtUtc).HasColumnName("uploaded_at_utc");
        builder.Property(a => a.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(a => a.HardDeletedAtUtc).HasColumnName("hard_deleted_at_utc");

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.IsAccessible);

        builder.HasIndex(a => a.OwnerId).HasDatabaseName("ix_media_assets_owner_id");
        builder.HasIndex(a => a.State).HasDatabaseName("ix_media_assets_state");
        builder.HasIndex(a => a.DeletedAtUtc).HasDatabaseName("ix_media_assets_deleted_at_utc");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
    }
}
