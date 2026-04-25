using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Presence.Domain.Aggregates;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence.Configurations;

public sealed class LastSeenConfiguration : IEntityTypeConfiguration<LastSeen>
{
    public void Configure(EntityTypeBuilder<LastSeen> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("last_seen");

        builder.HasKey(ls => ls.UserId);
        builder.Property(ls => ls.UserId).HasColumnName("user_id").ValueGeneratedNever();

        builder.Property(ls => ls.LastSeenAt).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(ls => ls.LastPlatform).HasColumnName("last_platform").HasConversion<int>().IsRequired();

        builder.Ignore(ls => ls.DomainEvents);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
    }
}
