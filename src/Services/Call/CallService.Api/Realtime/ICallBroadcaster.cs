using Urfu.Link.Services.Call.Application.Contracts;

namespace Urfu.Link.Services.Call.Realtime;

public interface ICallBroadcaster
{
    Task NotifyIncomingAsync(IReadOnlyList<Guid> recipientUserIds, CallSessionDto call, CancellationToken cancellationToken);

    Task NotifyAcceptedAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken);

    Task NotifyDeclinedAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken);

    Task NotifyCancelledAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken);

    Task NotifyParticipantJoinedAsync(IReadOnlyList<Guid> participantUserIds, Guid callId, Guid userId, CancellationToken cancellationToken);

    Task NotifyParticipantLeftAsync(IReadOnlyList<Guid> participantUserIds, Guid callId, Guid userId, CancellationToken cancellationToken);

    Task NotifyEndedAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken);
}
