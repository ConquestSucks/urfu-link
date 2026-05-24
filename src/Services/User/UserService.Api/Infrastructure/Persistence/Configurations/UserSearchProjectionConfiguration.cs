using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Api.Domain;

namespace UserService.Api.Infrastructure.Persistence.Configurations;

public sealed class UserSearchProjectionConfiguration : IEntityTypeConfiguration<UserSearchProjection>
{
    public void Configure(EntityTypeBuilder<UserSearchProjection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_search_projection");

        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();

        // citext — case-insensitive text. Использовать btree-unique индексы по
        // lower(username)/lower(email) можно и поверх text-колонок, но citext
        // даёт прямые сравнения '=' без обёрток в lower() везде, где сравниваем.
        builder.Property(x => x.Username).HasColumnName("username").HasColumnType("citext").IsRequired();
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasColumnType("citext");
        builder.Property(x => x.LastName).HasColumnName("last_name").HasColumnType("citext");
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("citext");
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasColumnType("citext").IsRequired();

        builder.Property(x => x.SearchText).HasColumnName("search_text").IsRequired();
        builder.Property(x => x.SearchTextTranslit).HasColumnName("search_text_translit").IsRequired();

        builder.Property(x => x.KeycloakModifiedMs).HasColumnName("keycloak_modified_ms").IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        // tsvector-колонка управляется триггером в миграции (см. AddUserSearchProjection).
        // EF про неё знать не должен — иначе SaveChanges пытался бы её сериализовать.
        // Доступ к ней — только через raw SQL в PgUserSearchRepository.
    }
}
