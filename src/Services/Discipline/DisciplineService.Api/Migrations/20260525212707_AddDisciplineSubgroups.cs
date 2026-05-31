using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DisciplineService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplineSubgroups : Migration
    {
        private static readonly string[] EnrollmentDisciplineSubgroupColumns = ["discipline_id", "subgroup_id"];
        private static readonly string[] SubgroupDisciplineNameColumns = ["discipline_id", "name"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<Guid>(
                name: "subgroup_id",
                schema: "disciplines",
                table: "enrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "subgroups",
                schema: "disciplines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subgroups", x => x.id);
                    table.ForeignKey(
                        name: "FK_subgroups_disciplines_discipline_id",
                        column: x => x.discipline_id,
                        principalSchema: "disciplines",
                        principalTable: "disciplines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql(
                """
                INSERT INTO disciplines.subgroups (id, discipline_id, name, created_at_utc, updated_at_utc, archived_at_utc)
                SELECT gen_random_uuid(), d.id, 'Подгруппа 1', d.created_at_utc, d.updated_at_utc, NULL
                FROM disciplines.disciplines d
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM disciplines.subgroups s
                    WHERE s.discipline_id = d.id
                      AND s.archived_at_utc IS NULL
                );
                """);
            migrationBuilder.Sql(
                """
                UPDATE disciplines.enrollments e
                SET subgroup_id = s.id
                FROM disciplines.subgroups s
                WHERE s.discipline_id = e.discipline_id
                  AND s.name = 'Подгруппа 1'
                  AND s.archived_at_utc IS NULL
                  AND e.role = 1
                  AND e.subgroup_id IS NULL;
                """);
            migrationBuilder.Sql(
                """
                ALTER TABLE disciplines.enrollments
                ADD CONSTRAINT ck_enrollments_role_subgroup
                CHECK (
                    (role = 0 AND subgroup_id IS NULL)
                    OR
                    (role = 1 AND subgroup_id IS NOT NULL)
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_discipline_id_subgroup_id",
                schema: "disciplines",
                table: "enrollments",
                columns: EnrollmentDisciplineSubgroupColumns);

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_subgroup_id",
                schema: "disciplines",
                table: "enrollments",
                column: "subgroup_id");

            migrationBuilder.CreateIndex(
                name: "IX_subgroups_archived_at_utc",
                schema: "disciplines",
                table: "subgroups",
                column: "archived_at_utc",
                filter: "archived_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_subgroups_discipline_id_name",
                schema: "disciplines",
                table: "subgroups",
                columns: SubgroupDisciplineNameColumns,
                unique: true,
                filter: "archived_at_utc IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_enrollments_subgroups_subgroup_id",
                schema: "disciplines",
                table: "enrollments",
                column: "subgroup_id",
                principalSchema: "disciplines",
                principalTable: "subgroups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("ALTER TABLE disciplines.enrollments DROP CONSTRAINT IF EXISTS ck_enrollments_role_subgroup;");

            migrationBuilder.DropForeignKey(
                name: "FK_enrollments_subgroups_subgroup_id",
                schema: "disciplines",
                table: "enrollments");

            migrationBuilder.DropTable(
                name: "subgroups",
                schema: "disciplines");

            migrationBuilder.DropIndex(
                name: "IX_enrollments_discipline_id_subgroup_id",
                schema: "disciplines",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "IX_enrollments_subgroup_id",
                schema: "disciplines",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "subgroup_id",
                schema: "disciplines",
                table: "enrollments");
        }
    }
}
