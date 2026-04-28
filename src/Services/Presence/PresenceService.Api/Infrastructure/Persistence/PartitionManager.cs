using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence;

/// <summary>
/// Rolls monthly partitions for <c>presence.last_seen_history</c>. The partitioning is
/// already created in the initial migration with three months pre-seeded; this manager
/// keeps the rolling window healthy at runtime so writes never hit a missing partition
/// at month boundaries, and drops partitions past the retention window.
/// </summary>
public sealed class PartitionManager(PresenceDbContext db)
{
    public const string ParentTable = "last_seen_history";

    public async Task EnsureAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var start = month.StartUtc().ToString("o", CultureInfo.InvariantCulture);
        var end = month.NextStartUtc().ToString("o", CultureInfo.InvariantCulture);
        var partition = $"{ParentTable}_{month.PartitionSuffix()}";

        var sql = $$"""
            CREATE TABLE IF NOT EXISTS {{PresenceDbContext.Schema}}.{{partition}}
            PARTITION OF {{PresenceDbContext.Schema}}.{{ParentTable}}
            FOR VALUES FROM ('{{start}}') TO ('{{end}}');
            """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    public async Task DropAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var partition = $"{ParentTable}_{month.PartitionSuffix()}";
        var sql = $"DROP TABLE IF EXISTS {PresenceDbContext.Schema}.{partition}";
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
            .SqlQueryRaw<string>(Sql, PresenceDbContext.Schema, ParentTable)
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

            if (YearMonth.TryParseSuffix(name[prefix.Length..], out var month))
            {
                result.Add(month);
            }
        }

        return result;
    }
}

/// <summary>
/// Year+month tuple keying monthly partitions of <c>last_seen_history</c>.
/// </summary>
public readonly record struct YearMonth(int Year, int Month)
{
    public static YearMonth FromUtc(DateTimeOffset moment) => new(moment.Year, moment.Month);

    public DateTimeOffset StartUtc() => new(Year, Month, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset NextStartUtc()
    {
        return Month == 12
            ? new DateTimeOffset(Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(Year, Month + 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    public string PartitionSuffix() =>
        string.Format(CultureInfo.InvariantCulture, "y{0}m{1:D2}", Year, Month);

    public static bool TryParseSuffix(string suffix, out YearMonth month)
    {
        month = default;
        if (string.IsNullOrEmpty(suffix) || suffix.Length < 7 || suffix[0] != 'y')
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
