using System.Globalization;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence;

public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth FromUtc(DateTimeOffset utc) => new(utc.Year, utc.Month);

    public YearMonth AddMonths(int months)
    {
        var anchor = new DateTime(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(months);
        return new YearMonth(anchor.Year, anchor.Month);
    }

    public DateTimeOffset StartUtc() => new(Year, Month, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset NextStartUtc() => AddMonths(1).StartUtc();

    public string PartitionSuffix() =>
        $"y{Year.ToString(CultureInfo.InvariantCulture)}m{Month.ToString("D2", CultureInfo.InvariantCulture)}";

    public int CompareTo(YearMonth other)
    {
        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    public static bool operator <(YearMonth a, YearMonth b) => a.CompareTo(b) < 0;

    public static bool operator >(YearMonth a, YearMonth b) => a.CompareTo(b) > 0;

    public static bool operator <=(YearMonth a, YearMonth b) => a.CompareTo(b) <= 0;

    public static bool operator >=(YearMonth a, YearMonth b) => a.CompareTo(b) >= 0;
}
