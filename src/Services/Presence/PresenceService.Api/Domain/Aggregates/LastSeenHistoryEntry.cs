using Urfu.Link.Services.Presence.Domain.Enums;

namespace Urfu.Link.Services.Presence.Domain.Aggregates;

/// <summary>
/// Append-only audit row for the user's last-seen transitions. The snapshot table
/// (<see cref="LastSeen"/>) is over-written on every disconnect; this table preserves
/// the history so an analytics query can answer "when was user X online during March?"
/// without joining Kafka logs. Partitioned by month on <see cref="RecordedAtUtc"/>; the
/// rolling-window worker creates partitions ahead and drops them past the retention
/// window.
/// </summary>
public sealed class LastSeenHistoryEntry
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public Platform LastPlatform { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    private LastSeenHistoryEntry() { }

    public static LastSeenHistoryEntry Record(
        Guid userId,
        DateTimeOffset lastSeenAtUtc,
        Platform platform,
        DateTimeOffset recordedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new LastSeenHistoryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LastSeenAtUtc = lastSeenAtUtc,
            LastPlatform = platform,
            RecordedAtUtc = recordedAtUtc,
        };
    }
}
