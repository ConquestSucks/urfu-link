using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaService.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.EnsureSchema(
                name: "media");

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    bucket = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hard_deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "upload_sessions",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_access_grants",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_access_grants", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_access_grants_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_access_grants_asset_user",
                schema: "media",
                table: "media_access_grants",
                columns: new[] { "asset_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_media_access_grants_source",
                schema: "media",
                table: "media_access_grants",
                columns: new[] { "source", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ux_media_access_grants_unique",
                schema: "media",
                table: "media_access_grants",
                columns: new[] { "asset_id", "user_id", "source", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_deleted_at_utc",
                schema: "media",
                table: "media_assets",
                column: "deleted_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_owner_id",
                schema: "media",
                table: "media_assets",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_state",
                schema: "media",
                table: "media_assets",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_is_completed_expires_at_utc",
                schema: "media",
                table: "upload_sessions",
                columns: new[] { "is_completed", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_upload_sessions_asset_id",
                schema: "media",
                table: "upload_sessions",
                column: "asset_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "media_access_grants",
                schema: "media");

            migrationBuilder.DropTable(
                name: "upload_sessions",
                schema: "media");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "media");
        }
    }
}
