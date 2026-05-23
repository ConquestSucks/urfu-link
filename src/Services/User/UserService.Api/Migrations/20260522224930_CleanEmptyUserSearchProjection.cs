using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Api.Migrations
{
    /// <summary>
    /// Очищает записи user_search_projection с пустым/whitespace display_name.
    /// Такие записи могли появиться от ранних lazy-upsert-ов без полноценных
    /// JWT-клеймов или from-Keycloak fallback-ов до фикса. Кэшируют "мусор"
    /// и блокируют последующие KC-fallback-и в BatchGetUsers.
    /// Удалённые id будут пересозданы reconciler-ом / lazy-upsert-ом.
    /// </summary>
    public partial class CleanEmptyUserSearchProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(@"
                DELETE FROM users.user_search_projection
                WHERE display_name IS NULL OR btrim(display_name::text) = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: восстанавливать удалённые «пустые» записи бессмысленно.
        }
    }
}
