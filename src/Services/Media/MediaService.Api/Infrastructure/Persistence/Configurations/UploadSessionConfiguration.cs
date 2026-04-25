using MediaService.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaService.Api.Infrastructure.Persistence.Configurations;

public sealed class UploadSessionConfiguration : IEntityTypeConfiguration<UploadSession>
{
    public void Configure(EntityTypeBuilder<UploadSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("upload_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.AssetId).HasColumnName("asset_id").IsRequired();
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(s => s.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(s => s.IsCompleted).HasColumnName("is_completed").IsRequired();

        builder.HasIndex(s => s.AssetId).IsUnique().HasDatabaseName("ux_upload_sessions_asset_id");
        builder.HasIndex(s => new { s.IsCompleted, s.ExpiresAtUtc })
            .HasDatabaseName("ix_upload_sessions_is_completed_expires_at_utc");
    }
}
