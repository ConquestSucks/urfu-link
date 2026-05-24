using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.EnsureSchema(name: "notifications");

            // Notifications: monthly RANGE partitioning by created_at_utc.
            migrationBuilder.Sql("""
                CREATE TABLE notifications.notifications (
                    id                 UUID NOT NULL,
                    recipient_user_id  UUID NOT NULL,
                    category           SMALLINT NOT NULL,
                    severity           SMALLINT NOT NULL,
                    title              VARCHAR(200) NOT NULL,
                    body               VARCHAR(1000) NOT NULL,
                    image_url          VARCHAR(2048),
                    deep_link          VARCHAR(2048),
                    data               JSONB NOT NULL DEFAULT '{}'::jsonb,
                    group_key          VARCHAR(200),
                    source_event_id    UUID NOT NULL,
                    source_event_type  VARCHAR(128) NOT NULL,
                    created_at_utc     TIMESTAMPTZ NOT NULL,
                    read_at_utc        TIMESTAMPTZ,
                    CONSTRAINT pk_notifications PRIMARY KEY (id, created_at_utc),
                    CONSTRAINT ux_notifications_idempotency UNIQUE (source_event_id, recipient_user_id, created_at_utc)
                ) PARTITION BY RANGE (created_at_utc);

                CREATE INDEX ix_notifications_recipient_created
                    ON notifications.notifications (recipient_user_id, created_at_utc DESC);

                CREATE INDEX ix_notifications_recipient_unread
                    ON notifications.notifications (recipient_user_id)
                    WHERE read_at_utc IS NULL;
                """);

            // Deliveries: not partitioned — delivery records expire with their notification via cascade
            // cleanup in the retention worker. They are smaller and benefit from cross-partition lookups.
            migrationBuilder.CreateTable(
                name: "deliveries",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    provider = table.Column<short>(type: "smallint", nullable: true),
                    push_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    last_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    skip_reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                },
                constraints: table => table.PrimaryKey("pk_deliveries", x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_channel_status_next_attempt",
                schema: "notifications",
                table: "deliveries",
                columns: ["channel", "status", "next_attempt_at_utc"]);

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_notification",
                schema: "notifications",
                table: "deliveries",
                column: "notification_id");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                },
                constraints: table => table.PrimaryKey("pk_outbox_messages", x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "notifications",
                table: "outbox_messages",
                columns: ["published_at_utc", "next_attempt_at_utc"]);

            migrationBuilder.CreateTable(
                name: "push_devices",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<short>(type: "smallint", nullable: false),
                    token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    device_fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    app_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivation_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    deactivated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table => table.PrimaryKey("pk_push_devices", x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_push_devices_user_active",
                schema: "notifications",
                table: "push_devices",
                columns: ["user_id", "is_active"]);

            migrationBuilder.CreateIndex(
                name: "ux_push_devices_user_provider_token",
                schema: "notifications",
                table: "push_devices",
                columns: ["user_id", "provider", "token"],
                unique: true);

            // Seed the current month and the next two months so writes immediately route into a partition.
            var seedMonths = new[]
            {
                new YearMonthSeed(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
                Add(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                Add(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 2),
            };

            foreach (var month in seedMonths)
            {
                var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    .ToString("o", CultureInfo.InvariantCulture);
                var nextDate = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                var end = nextDate.ToString("o", CultureInfo.InvariantCulture);
                var suffix = $"y{month.Year.ToString(CultureInfo.InvariantCulture)}m{month.Month.ToString("D2", CultureInfo.InvariantCulture)}";

                migrationBuilder.Sql(string.Format(
                    CultureInfo.InvariantCulture,
                    "CREATE TABLE notifications.notifications_{0} PARTITION OF notifications.notifications FOR VALUES FROM ('{1}') TO ('{2}');",
                    suffix,
                    start,
                    end));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(name: "deliveries", schema: "notifications");
            migrationBuilder.DropTable(name: "outbox_messages", schema: "notifications");
            migrationBuilder.DropTable(name: "push_devices", schema: "notifications");
            // Drop notifications and all its child partitions.
            migrationBuilder.Sql("DROP TABLE IF EXISTS notifications.notifications CASCADE;");
        }

        private static YearMonthSeed Add(int year, int month, int delta)
        {
            var date = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(delta);
            return new YearMonthSeed(date.Year, date.Month);
        }

        private sealed record YearMonthSeed(int Year, int Month);
    }
}
