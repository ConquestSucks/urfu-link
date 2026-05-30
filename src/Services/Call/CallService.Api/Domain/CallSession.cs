using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

namespace Urfu.Link.Services.Call.Domain;

public sealed record CallSession(
    Guid Id,
    string ConversationId,
    Guid CallerId,
    IReadOnlyList<Guid> ParticipantIds,
    CallType CallType,
    CallStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset RingExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? EndedAtUtc,
    CallEndReason? EndReason,
    IReadOnlyList<Guid> ConnectedParticipantIds)
{
    public bool IsParticipant(Guid userId) => ParticipantIds.Contains(userId);

    public TimeSpan DurationUntil(DateTimeOffset nowUtc)
        => AcceptedAtUtc is { } acceptedAt
            ? nowUtc - acceptedAt
            : TimeSpan.Zero;
}
