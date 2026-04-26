using DisciplineService.Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisciplineService.Api.Infrastructure.Persistence.Configurations;

public sealed class DisciplineConfiguration : IEntityTypeConfiguration<Discipline>
{
    public void Configure(EntityTypeBuilder<Discipline> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("disciplines");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsRequired();
        builder.HasIndex(d => d.Code).IsUnique();

        builder.Property(d => d.Title)
            .HasColumnName("title")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(d => d.Semester)
            .HasColumnName("semester")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.OwnerTeacherId).HasColumnName("owner_teacher_id");
        builder.Property(d => d.CoverAssetId).HasColumnName("cover_asset_id");

        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(d => d.ArchivedAtUtc).HasColumnName("archived_at_utc");

        builder.HasIndex(d => d.Semester);
        builder.HasIndex(d => d.OwnerTeacherId);
        builder.HasIndex(d => d.ArchivedAtUtc).HasFilter("archived_at_utc IS NULL");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasMany(d => d.Enrollments)
            .WithOne()
            .HasForeignKey(e => e.DisciplineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Discipline.Enrollments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.IsArchived);
    }
}
