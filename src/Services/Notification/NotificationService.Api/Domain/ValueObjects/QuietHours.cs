namespace Urfu.Link.Services.Notification.Domain.ValueObjects;

public sealed record QuietHours
{
    public string IanaTimezone { get; }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public bool Enabled { get; }

    private QuietHours(string ianaTimezone, TimeOnly start, TimeOnly end, bool enabled)
    {
        IanaTimezone = ianaTimezone;
        Start = start;
        End = end;
        Enabled = enabled;
    }

    public static QuietHours Disabled(string ianaTimezone)
    {
        ValidateTimezone(ianaTimezone);
        return new QuietHours(ianaTimezone, TimeOnly.MinValue, TimeOnly.MinValue, enabled: false);
    }

    public static QuietHours Create(string ianaTimezone, TimeOnly start, TimeOnly end)
    {
        ValidateTimezone(ianaTimezone);
        if (start == end)
        {
            throw new ArgumentException("Quiet hours start and end must differ.", nameof(start));
        }

        return new QuietHours(ianaTimezone, start, end, enabled: true);
    }

    public bool IsActive(DateTimeOffset utcNow)
    {
        if (!Enabled)
        {
            return false;
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(IanaTimezone);
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, tz).DateTime);

        // Window crosses midnight (e.g. 22:00 — 08:00) when Start > End.
        return Start < End
            ? localTime >= Start && localTime < End
            : localTime >= Start || localTime < End;
    }

    private static void ValidateTimezone(string ianaTimezone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaTimezone);
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ArgumentException($"Unknown IANA timezone '{ianaTimezone}'.", nameof(ianaTimezone), ex);
        }
    }
}
