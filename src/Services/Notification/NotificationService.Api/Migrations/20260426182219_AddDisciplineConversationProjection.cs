using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplineConversationProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "discipline_conversations",
                schema: "notifications",
                columns: table => new
                {
                    conversation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discipline_conversations", x => x.conversation_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_discipline_conversations_discipline",
                schema: "notifications",
                table: "discipline_conversations",
                column: "discipline_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "discipline_conversations",
                schema: "notifications");
        }
    }
}
