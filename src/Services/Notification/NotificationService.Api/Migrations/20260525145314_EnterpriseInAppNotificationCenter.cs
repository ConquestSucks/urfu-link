using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseInAppNotificationCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "actions",
                schema: "notifications",
                table: "notifications",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "actor",
                schema: "notifications",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "done_at_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity",
                schema: "notifications",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_occurrence_at_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "occurrence_count",
                schema: "notifications",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "saved_at_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "seen_at_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "snoozed_until_utc",
                schema: "notifications",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "notifications",
                table: "notifications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "system.update");

            migrationBuilder.Sql(
                """
                UPDATE notifications.notifications
                SET last_occurrence_at_utc = created_at_utc,
                    type = CASE category
                        WHEN 1 THEN 'chat.message.direct'
                        WHEN 2 THEN 'chat.mention'
                        WHEN 3 THEN 'chat.message.discipline'
                        WHEN 10 THEN 'call.incoming'
                        WHEN 11 THEN 'call.missed'
                        WHEN 20 THEN 'discipline.announcement'
                        WHEN 21 THEN 'discipline.material'
                        WHEN 22 THEN 'discipline.deadline'
                        WHEN 30 THEN 'system.maintenance'
                        WHEN 31 THEN 'system.update'
                        WHEN 40 THEN 'admin.chat.invite'
                        WHEN 41 THEN 'admin.role.changed'
                        ELSE 'system.update'
                    END;
                """);

            migrationBuilder.CreateTable(
                name: "notification_dedup_keys",
                schema: "notifications",
                columns: table => new
                {
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_dedup_keys", x => new { x.recipient_user_id, x.source_event_id, x.notification_type });
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_dedup_keys_notification",
                schema: "notifications",
                table: "notification_dedup_keys",
                column: "notification_id");

            migrationBuilder.Sql(
                """
                INSERT INTO notifications.notification_dedup_keys (
                    recipient_user_id,
                    source_event_id,
                    notification_type,
                    notification_id,
                    created_at_utc)
                SELECT DISTINCT ON (recipient_user_id, source_event_id, type)
                    recipient_user_id,
                    source_event_id,
                    type,
                    id,
                    created_at_utc
                FROM notifications.notifications
                ORDER BY recipient_user_id, source_event_id, type, created_at_utc DESC
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS ix_notifications_recipient_status_created
                    ON notifications.notifications (
                        recipient_user_id,
                        done_at_utc,
                        archived_at_utc,
                        read_at_utc,
                        created_at_utc DESC);

                CREATE INDEX IF NOT EXISTS ix_notifications_recipient_type_created
                    ON notifications.notifications (
                        recipient_user_id,
                        type,
                        created_at_utc DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "notification_dedup_keys",
                schema: "notifications");

            migrationBuilder.Sql("DROP INDEX IF EXISTS notifications.ix_notifications_recipient_status_created;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS notifications.ix_notifications_recipient_type_created;");

            migrationBuilder.DropColumn(
                name: "actions",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "actor",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "done_at_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "entity",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "expires_at_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "last_occurrence_at_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "occurrence_count",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "saved_at_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "seen_at_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "snoozed_until_utc",
                schema: "notifications",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "notifications",
                table: "notifications");
        }
    }
}
