using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Aggregates;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class DisciplineConversationConfiguration : IEntityTypeConfiguration<DisciplineConversation>
{
    public void Configure(EntityTypeBuilder<DisciplineConversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("discipline_conversations");
        builder.HasKey(x => x.ConversationId);
        builder.Property(x => x.ConversationId).HasColumnName("conversation_id").HasMaxLength(200);
        builder.Property(x => x.DisciplineId).HasColumnName("discipline_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(x => x.DisciplineId).HasDatabaseName("ix_discipline_conversations_discipline");
    }
}
