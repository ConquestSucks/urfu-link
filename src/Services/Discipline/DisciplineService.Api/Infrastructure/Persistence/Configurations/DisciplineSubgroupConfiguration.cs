using DisciplineService.Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisciplineService.Api.Infrastructure.Persistence.Configurations;

public sealed class DisciplineSubgroupConfiguration : IEntityTypeConfiguration<DisciplineSubgroup>
{
    public void Configure(EntityTypeBuilder<DisciplineSubgroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subgroups");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.DisciplineId).HasColumnName("discipline_id");
        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(s => s.ArchivedAtUtc).HasColumnName("archived_at_utc");

        builder.HasIndex(s => new { s.DisciplineId, s.Name })
            .HasFilter("archived_at_utc IS NULL")
            .IsUnique();
        builder.HasIndex(s => s.ArchivedAtUtc)
            .HasFilter("archived_at_utc IS NULL");

        builder.Ignore(s => s.IsArchived);
    }
}
