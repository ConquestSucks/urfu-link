using DisciplineService.Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisciplineService.Api.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("enrollments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.DisciplineId).HasColumnName("discipline_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<int>();
        builder.Property(e => e.EnrolledAtUtc).HasColumnName("enrolled_at_utc");
        builder.Property(e => e.EnrolledBy).HasColumnName("enrolled_by");

        builder.HasIndex(e => new { e.DisciplineId, e.UserId }).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.Role });

        // xmin concurrency token: tightens optimistic locking from "any change to the
        // discipline aggregate" down to the specific enrollment row. Two operators
        // promoting the same student concurrently now race on the row instead of
        // squashing each other through the aggregate token.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
    }
}
