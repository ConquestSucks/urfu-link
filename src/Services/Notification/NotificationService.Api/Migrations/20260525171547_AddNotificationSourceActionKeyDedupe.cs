using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSourceActionKeyDedupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<short>(
                name: "priority",
                schema: "notifications",
                table: "notifications",
                type: "smallint",
                nullable: false,
                defaultValue: (short)20);

            migrationBuilder.AddColumn<string>(
                name: "source_action_id",
                schema: "notifications",
                table: "notifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "superseded_by_notification_id",
                schema: "notifications",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_source_action_keys",
                schema: "notifications",
                columns: table => new
                {
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_action_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_source_action_keys", x => new { x.recipient_user_id, x.source_action_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_source_action_keys_notification",
                schema: "notifications",
                table: "notification_source_action_keys",
                column: "notification_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "notification_source_action_keys",
                schema: "notifications");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "source_action_id",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "superseded_by_notification_id",
                schema: "notifications",
                table: "notifications");
        }
    }
}
