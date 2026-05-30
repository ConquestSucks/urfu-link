using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Call.Domain;

namespace Urfu.Link.Services.Call.Application.Contracts;

public sealed record StartCallRequest(CallType CallType);

public sealed record CallParticipantDto(Guid UserId, bool IsConnected);

public sealed record CallSessionDto(
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
    IReadOnlyList<CallParticipantDto> Participants)
{
    public static CallSessionDto FromDomain(CallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var connected = session.ConnectedParticipantIds.ToHashSet();
        return new CallSessionDto(
            session.Id,
            session.ConversationId,
            session.CallerId,
            session.ParticipantIds,
            session.CallType,
            session.Status,
            session.CreatedAtUtc,
            session.RingExpiresAtUtc,
            session.AcceptedAtUtc,
            session.EndedAtUtc,
            session.EndReason,
            session.ParticipantIds
                .Select(id => new CallParticipantDto(id, connected.Contains(id)))
                .ToList());
    }
}

public sealed record CallTokenDto(
    Guid CallId,
    string ServerUrl,
    string RoomName,
    string Token,
    DateTimeOffset ExpiresAtUtc);
