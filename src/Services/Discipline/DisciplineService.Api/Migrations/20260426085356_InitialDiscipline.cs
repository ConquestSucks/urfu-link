using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DisciplineService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialDiscipline : Migration
    {
        private static readonly string[] EnrollmentDisciplineUserIndexColumns = ["discipline_id", "user_id"];
        private static readonly string[] EnrollmentUserRoleIndexColumns = ["user_id", "role"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.EnsureSchema(
                name: "disciplines");

            migrationBuilder.CreateTable(
                name: "disciplines",
                schema: "disciplines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    semester = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cover_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disciplines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                schema: "disciplines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    enrolled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enrolled_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "FK_enrollments_disciplines_discipline_id",
                        column: x => x.discipline_id,
                        principalSchema: "disciplines",
                        principalTable: "disciplines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disciplines_archived_at_utc",
                schema: "disciplines",
                table: "disciplines",
                column: "archived_at_utc",
                filter: "archived_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_disciplines_code",
                schema: "disciplines",
                table: "disciplines",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disciplines_owner_teacher_id",
                schema: "disciplines",
                table: "disciplines",
                column: "owner_teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_disciplines_semester",
                schema: "disciplines",
                table: "disciplines",
                column: "semester");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_discipline_id_user_id",
                schema: "disciplines",
                table: "enrollments",
                columns: EnrollmentDisciplineUserIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_user_id_role",
                schema: "disciplines",
                table: "enrollments",
                columns: EnrollmentUserRoleIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "enrollments",
                schema: "disciplines");

            migrationBuilder.DropTable(
                name: "disciplines",
                schema: "disciplines");
        }
    }
}
