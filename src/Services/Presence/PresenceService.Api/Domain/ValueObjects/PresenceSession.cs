using Urfu.Link.Services.Presence.Domain.Enums;

namespace Urfu.Link.Services.Presence.Domain.ValueObjects;

public sealed record PresenceSession(
    Guid UserId,
    string DeviceId,
    Platform Platform,
    PresenceStatus Status,
    string? CustomActivity,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastHeartbeatAt,
    string? ConnectionId = null,
    IReadOnlyList<string>? ViewingContexts = null);
