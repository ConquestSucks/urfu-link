namespace UserService.Api.Domain.ValueObjects;

public sealed record QuietHours(string IanaTimezone, TimeOnly? Start, TimeOnly? End, bool Enabled)
{
    public static QuietHours Default { get; } = new("Asia/Yekaterinburg", null, null, false);

    public static QuietHours Create(string ianaTimezone, TimeOnly start, TimeOnly end)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaTimezone);
        ValidateTimezone(ianaTimezone);
        if (start == end)
        {
            throw new ArgumentException("Quiet hours start and end must differ.", nameof(start));
        }

        return new QuietHours(ianaTimezone, start, end, true);
    }

    public static QuietHours Disabled(string ianaTimezone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaTimezone);
        ValidateTimezone(ianaTimezone);
        return new QuietHours(ianaTimezone, null, null, false);
    }

    private static void ValidateTimezone(string ianaTimezone)
    {
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
