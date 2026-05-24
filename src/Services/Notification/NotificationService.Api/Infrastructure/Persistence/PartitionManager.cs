using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence;

/// <summary>
/// Manages monthly range partitions for <c>notifications.notifications</c>. Ensures
/// partitions exist for incoming writes and drops partitions older than the configured
/// retention window. Deliveries are not partitioned — they share retention with their
/// parent notification.
/// </summary>
public sealed class PartitionManager(NotificationDbContext db)
{
    public const string ParentTable = "notifications";

    public async Task EnsureAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var start = month.StartUtc().ToString("o", CultureInfo.InvariantCulture);
        var end = month.NextStartUtc().ToString("o", CultureInfo.InvariantCulture);
        var suffix = month.PartitionSuffix();
        var partition = $"{ParentTable}_{suffix}";

        var sql = $$"""
            CREATE TABLE IF NOT EXISTS {{NotificationDbContext.Schema}}.{{partition}}
            PARTITION OF {{NotificationDbContext.Schema}}.{{ParentTable}}
            FOR VALUES FROM ('{{start}}') TO ('{{end}}');
            """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    public async Task DropAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var suffix = month.PartitionSuffix();
        var partition = $"{ParentTable}_{suffix}";
        var sql = $"DROP TABLE IF EXISTS {NotificationDbContext.Schema}.{partition}";
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<YearMonth>> ListAsync(CancellationToken cancellationToken)
    {
        const string Sql = """
            SELECT child.relname
            FROM pg_inherits
            JOIN pg_class parent ON pg_inherits.inhparent = parent.oid
            JOIN pg_namespace ns ON parent.relnamespace = ns.oid
            JOIN pg_class child ON pg_inherits.inhrelid = child.oid
            WHERE ns.nspname = {0}
              AND parent.relname = {1}
            ORDER BY child.relname;
            """;

        var names = await db.Database
            .SqlQueryRaw<string>(Sql, NotificationDbContext.Schema, ParentTable)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var prefix = ParentTable + "_";
        var result = new List<YearMonth>(names.Count);
        foreach (var name in names)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = name[prefix.Length..];
            if (TryParseSuffix(suffix, out var month))
            {
                result.Add(month);
            }
        }

        return result;
    }

    private static bool TryParseSuffix(string suffix, out YearMonth month)
    {
        // Format: yYYYYmMM
        month = default;
        if (suffix.Length < 7 || suffix[0] != 'y')
        {
            return false;
        }

        var separator = suffix.IndexOf('m', StringComparison.Ordinal);
        if (separator < 5)
        {
            return false;
        }

        if (!int.TryParse(suffix[1..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (!int.TryParse(suffix[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var monthNumber))
        {
            return false;
        }

        if (monthNumber is < 1 or > 12)
        {
            return false;
        }

        month = new YearMonth(year, monthNumber);
        return true;
    }
}
