namespace Urfu.Link.Services.Presence.Infrastructure;

public sealed class PresenceOptions
{
    public const string SectionName = "Presence";

    /// <summary>How long a session stays alive in Redis without a heartbeat.</summary>
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Recommended interval at which clients should send heartbeats.</summary>
    public TimeSpan HeartbeatExpectedInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Per-conversation typing key TTL.</summary>
    public TimeSpan TypingTtl { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Sweeper poll interval.</summary>
    public TimeSpan SweeperInterval { get; set; } = TimeSpan.FromSeconds(10);
}
