using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Urfu.Link.Services.Notification.Endpoints;

internal static class Cursor
{
    public static string? Encode(DateTimeOffset createdAtUtc, Guid id)
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{createdAtUtc.UtcTicks:D}|{id:N}");
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToBase64String(bytes);
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAtUtc, out Guid id)
    {
        createdAtUtc = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var raw = Encoding.UTF8.GetString(bytes);
            var parts = raw.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return false;
            }

            if (!Guid.TryParse(parts[1], out id))
            {
                return false;
            }

            createdAtUtc = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
