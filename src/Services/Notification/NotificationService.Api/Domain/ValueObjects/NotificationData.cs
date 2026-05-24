namespace Urfu.Link.Services.Notification.Domain.ValueObjects;

public sealed class NotificationData : IEquatable<NotificationData>
{
    public const int MaxBytes = 4096;

    public static NotificationData Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, string> Values { get; }

    public int Count => Values.Count;

    private NotificationData(IReadOnlyDictionary<string, string> values)
    {
        Values = values;
    }

    public static NotificationData From(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Empty;
        }

        var copy = new Dictionary<string, string>(values.Count, StringComparer.Ordinal);
        var byteCount = 0;
        foreach (var (key, value) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);

            byteCount += System.Text.Encoding.UTF8.GetByteCount(key);
            byteCount += System.Text.Encoding.UTF8.GetByteCount(value);
            if (byteCount > MaxBytes)
            {
                throw new ArgumentException($"Notification data payload exceeds {MaxBytes} bytes.", nameof(values));
            }

            copy[key] = value;
        }

        return new NotificationData(copy);
    }

    public bool Equals(NotificationData? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Values.Count != other.Values.Count)
        {
            return false;
        }

        foreach (var (key, value) in Values)
        {
            if (!other.Values.TryGetValue(key, out var otherValue) || !string.Equals(value, otherValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is NotificationData other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (key, value) in Values.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            hash.Add(key);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
