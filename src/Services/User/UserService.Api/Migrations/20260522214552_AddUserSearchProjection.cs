using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSearchProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // pg_trgm — для prefix-match по «ива» и fuzzy через similarity().
            // unaccent — для нормализации латинских диакритиков на стороне БД.
            // citext — case-insensitive text (объявлено через EF Annotation выше,
            //          но дублируем CREATE IF NOT EXISTS на случай ручного отката).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS citext;");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "user_search_projection",
                schema: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "citext", nullable: false),
                    first_name = table.Column<string>(type: "citext", nullable: true),
                    last_name = table.Column<string>(type: "citext", nullable: true),
                    email = table.Column<string>(type: "citext", nullable: true),
                    display_name = table.Column<string>(type: "citext", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    search_text_translit = table.Column<string>(type: "text", nullable: false),
                    keycloak_modified_ms = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_search_projection", x => x.user_id);
                });

            // search_vector — GENERATED колонка из search_text, чтобы EF её не трогал,
            // а Postgres сам поддерживал в актуальном состоянии при UPSERT.
            // Конфигурация 'russian' для стемминга. Если в проде её нет —
            // достаточно создать через `CREATE TEXT SEARCH CONFIGURATION russian (COPY = simple);`,
            // но в стандартном postgres-image 'russian' уже идёт из коробки.
            migrationBuilder.Sql(@"
                ALTER TABLE users.user_search_projection
                ADD COLUMN search_vector tsvector
                GENERATED ALWAYS AS (to_tsvector('russian', coalesce(search_text, ''))) STORED;
            ");

            // GIN-индекс по tsvector — full-text поиск с ranking через ts_rank_cd.
            migrationBuilder.Sql(@"
                CREATE INDEX ix_user_search_projection_search_vector
                ON users.user_search_projection USING GIN (search_vector)
                WHERE deleted_at_utc IS NULL;
            ");

            // GIN trigram по основному поисковому тексту — prefix + fuzzy.
            migrationBuilder.Sql(@"
                CREATE INDEX ix_user_search_projection_search_text_trgm
                ON users.user_search_projection USING GIN (search_text gin_trgm_ops)
                WHERE deleted_at_utc IS NULL;
            ");

            // GIN trigram по транслитерированному тексту — раскладочная толерантность.
            migrationBuilder.Sql(@"
                CREATE INDEX ix_user_search_projection_search_text_translit_trgm
                ON users.user_search_projection USING GIN (search_text_translit gin_trgm_ops)
                WHERE deleted_at_utc IS NULL;
            ");

            // Уникальные exact-индексы для дешёвого username/email lookup.
            // citext уже case-insensitive, но WHERE NOT NULL нужен для email
            // (Keycloak допускает пустой email у системных аккаунтов).
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ux_user_search_projection_username
                ON users.user_search_projection (username)
                WHERE deleted_at_utc IS NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ux_user_search_projection_email
                ON users.user_search_projection (email)
                WHERE deleted_at_utc IS NULL AND email IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "user_search_projection",
                schema: "users");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            // Расширения и tsvector-колонка удаляются вместе с таблицей. Сами
            // CREATE EXTENSION не откатываем — это глобальное состояние БД,
            // его могут использовать другие миграции/проекты.
        }
    }
}
