using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Call.Application.Contracts;

namespace Urfu.Link.Services.Call.Realtime;

public sealed class CallBroadcaster(IHubContext<CallHub, ICallClient> hub) : ICallBroadcaster
{
    public Task NotifyIncomingAsync(IReadOnlyList<Guid> recipientUserIds, CallSessionDto call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        ArgumentNullException.ThrowIfNull(call);

        _ = cancellationToken;
        return recipientUserIds.Count == 0
            ? Task.CompletedTask
            : hub.Clients.Users(ToUserIds(recipientUserIds)).IncomingCall(call);
    }

    public Task NotifyAcceptedAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        ArgumentNullException.ThrowIfNull(call);

        _ = cancellationToken;
        return hub.Clients.Users(ToUserIds(participantUserIds)).CallAccepted(call);
    }

    public Task NotifyDeclinedAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        ArgumentNullException.ThrowIfNull(call);

        _ = cancellationToken;
        return hub.Clients.Users(ToUserIds(participantUserIds)).CallDeclined(call);
    }

    public Task NotifyCancelledAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        ArgumentNullException.ThrowIfNull(call);

        _ = cancellationToken;
        return hub.Clients.Users(ToUserIds(participantUserIds)).CallCancelled(call);
    }

    public Task NotifyParticipantJoinedAsync(IReadOnlyList<Guid> participantUserIds, Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);

        _ = cancellationToken;
        return hub.Clients.Users(ToUserIds(participantUserIds)).CallParticipantJoined(callId, userId);
    }

    public Task NotifyParticipantLeftAsync(IReadOnlyList<Guid> participantUserIds, Guid callId, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);

        _ = cancellationToken;
        return hub.Clients.Users(ToUserIds(participantUserIds)).CallParticipantLeft(callId, userId);
    }

    public Task NotifyEndedAsync(IReadOnlyList<Guid> participantUserIds, CallSessionDto call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        ArgumentNullException.ThrowIfNull(call);

        _ = cancellationToken;
        return hub.Clients.Users(ToUserIds(participantUserIds)).CallEnded(call);
    }

    private static List<string> ToUserIds(IReadOnlyList<Guid> userIds)
        => userIds.Select(u => u.ToString("D", CultureInfo.InvariantCulture)).ToList();
}
