using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresenceService.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSeenHistoryPartitioned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Postgres requires the partition key column to be part of the primary key on a
            // partitioned parent. Composite (id, recorded_at_utc) covers that constraint while
            // keeping point lookups by id efficient via the implicit index.
            migrationBuilder.Sql(
                """
                CREATE TABLE presence.last_seen_history (
                    id                 UUID NOT NULL,
                    user_id            UUID NOT NULL,
                    last_seen_at_utc   TIMESTAMPTZ NOT NULL,
                    last_platform      INTEGER NOT NULL,
                    recorded_at_utc    TIMESTAMPTZ NOT NULL,
                    CONSTRAINT pk_last_seen_history PRIMARY KEY (id, recorded_at_utc)
                ) PARTITION BY RANGE (recorded_at_utc);

                CREATE INDEX ix_last_seen_history_user_id
                    ON presence.last_seen_history (user_id);
                """);

            // Seed the current month and the next two months so writes immediately after
            // deployment have a target partition. The PartitionManager keeps this rolling.
            var now = DateTime.UtcNow;
            for (var offset = 0; offset < 3; offset++)
            {
                var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(offset);
                var nextMonth = month.AddMonths(1);
                var suffix = $"y{month.Year}m{month.Month:D2}";
                var start = month.ToString("yyyy-MM-dd 00:00:00+00", CultureInfo.InvariantCulture);
                var end = nextMonth.ToString("yyyy-MM-dd 00:00:00+00", CultureInfo.InvariantCulture);

                migrationBuilder.Sql(string.Format(
                    CultureInfo.InvariantCulture,
                    """
                    CREATE TABLE presence.last_seen_history_{0}
                    PARTITION OF presence.last_seen_history
                    FOR VALUES FROM ('{1}') TO ('{2}');
                    """,
                    suffix, start, end));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("DROP TABLE IF EXISTS presence.last_seen_history CASCADE;");
        }
    }
}
