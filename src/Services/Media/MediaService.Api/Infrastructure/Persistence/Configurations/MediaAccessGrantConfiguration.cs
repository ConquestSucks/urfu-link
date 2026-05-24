using MediaService.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaService.Api.Infrastructure.Persistence.Configurations;

public sealed class MediaAccessGrantConfiguration : IEntityTypeConfiguration<MediaAccessGrant>
{
    public void Configure(EntityTypeBuilder<MediaAccessGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("media_access_grants");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(g => g.AssetId).HasColumnName("asset_id").IsRequired();
        builder.Property(g => g.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(g => g.Source).HasColumnName("source").HasConversion<int>().IsRequired();
        builder.Property(g => g.SourceId).HasColumnName("source_id").HasMaxLength(128);
        builder.Property(g => g.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        builder.Property(g => g.GrantedAtUtc).HasColumnName("granted_at_utc").IsRequired();

        // Hot-path "can user X access asset Y?" lookup.
        builder.HasIndex(g => new { g.AssetId, g.UserId })
            .HasDatabaseName("ix_media_access_grants_asset_user");

        // Cascade revocation when conversation/discipline is deleted.
        builder.HasIndex(g => new { g.Source, g.SourceId })
            .HasDatabaseName("ix_media_access_grants_source");

        // Same (asset, user, source, sourceId) tuple should not be inserted twice.
        builder.HasIndex(g => new { g.AssetId, g.UserId, g.Source, g.SourceId })
            .IsUnique()
            .HasDatabaseName("ux_media_access_grants_unique");

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(g => g.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
