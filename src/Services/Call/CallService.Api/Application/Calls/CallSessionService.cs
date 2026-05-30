using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Call.Application.Chat;
using Urfu.Link.Services.Call.Application.Contracts;
using Urfu.Link.Services.Call.Domain;
using Urfu.Link.Services.Call.Realtime;

namespace Urfu.Link.Services.Call.Application.Calls;

public sealed class CallSessionService(
    IChatConversationClient chat,
    ICallSessionStore sessions,
    LiveKitTokenProvider tokenProvider,
    IOptions<CallOptions> options,
    IOptions<LiveKitOptions> liveKitOptions,
    TimeProvider timeProvider,
    ICallBroadcaster broadcaster,
    CallEventDispatcher events,
    ILogger<CallSessionService> logger)
{
    public async Task<CallSessionDto> StartAsync(
        string conversationId,
        Guid callerId,
        CallType callType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        var conversation = await chat.GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        EnsureDirectParticipantConversation(conversation, conversationId, callerId);

        var now = timeProvider.GetUtcNow();
        var session = new CallSession(
            Guid.NewGuid(),
            conversationId,
            callerId,
            conversation.ParticipantIds,
            callType,
            CallStatus.Ringing,
            now,
            now.Add(options.Value.RingTimeout),
            AcceptedAtUtc: null,
            EndedAtUtc: null,
            EndReason: null,
            ConnectedParticipantIds: [callerId]);

        await sessions.TryCreateAsync(session, options.Value.SessionTtl, cancellationToken).ConfigureAwait(false);

        await events.PublishAsync(
            new CallIncomingV2Event(
                session.Id,
                session.ConversationId,
                session.CallerId,
                session.ParticipantIds,
                session.CallType,
                now),
            cancellationToken).ConfigureAwait(false);

        var dto = CallSessionDto.FromDomain(session);
        var recipients = session.ParticipantIds.Where(id => id != callerId).ToList();
        await broadcaster.NotifyIncomingAsync(recipients, dto, cancellationToken).ConfigureAwait(false);
        return dto;
    }

    public async Task<CallSessionDto> GetAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        return CallSessionDto.FromDomain(session);
    }

    public async Task<CallTokenDto> CreateTokenAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        if (session.Status == CallStatus.Ended)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Call has already ended.");
        }

        var roomName = RoomNameFor(session.Id);
        var (token, expiresAt) = tokenProvider.CreateJoinToken(
            roomName,
            callerId,
            callerId.ToString("D"));

        return new CallTokenDto(session.Id, GetLiveKitServerUrl(), roomName, token, expiresAt);
    }

    public async Task<CallSessionDto> AcceptAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        if (session.Status == CallStatus.Ended)
        {
            return CallSessionDto.FromDomain(session);
        }

        if (session.Status == CallStatus.Active)
        {
            return CallSessionDto.FromDomain(session);
        }

        if (callerId == session.CallerId)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Caller cannot accept their own ringing call.");
        }

        var now = timeProvider.GetUtcNow();
        var active = session with
        {
            Status = CallStatus.Active,
            AcceptedAtUtc = now,
            ConnectedParticipantIds = AddConnected(session, callerId),
        };
        var saved = await sessions.TrySaveAsync(session, active, options.Value.SessionTtl, cancellationToken)
            .ConfigureAwait(false);
        if (!saved)
        {
            var current = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
            return CallSessionDto.FromDomain(current);
        }

        var dto = CallSessionDto.FromDomain(active);
        await broadcaster.NotifyAcceptedAsync(active.ParticipantIds, dto, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyParticipantJoinedAsync(active.ParticipantIds, active.Id, callerId, cancellationToken).ConfigureAwait(false);
        return dto;
    }

    public async Task<CallSessionDto> DeclineAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        if (callerId == session.CallerId)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Caller cannot decline their own call.");
        }

        if (session.Status == CallStatus.Ended)
        {
            return CallSessionDto.FromDomain(session);
        }

        if (session.Status != CallStatus.Ringing)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Only a ringing call can be declined.");
        }

        var (ended, changed) = await TryEndAsync(session, CallEndReason.DeclinedByCallee, cancellationToken)
            .ConfigureAwait(false);
        var dto = CallSessionDto.FromDomain(ended);
        if (changed)
        {
            await broadcaster.NotifyDeclinedAsync(ended.ParticipantIds, dto, cancellationToken).ConfigureAwait(false);
            await broadcaster.NotifyEndedAsync(ended.ParticipantIds, dto, cancellationToken).ConfigureAwait(false);
        }

        return dto;
    }

    public async Task<CallSessionDto> CancelAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        if (callerId != session.CallerId)
        {
            throw new CallProblemException(StatusCodes.Status403Forbidden, "Only the caller can cancel a ringing call.");
        }

        if (session.Status == CallStatus.Ended)
        {
            return CallSessionDto.FromDomain(session);
        }

        if (session.Status != CallStatus.Ringing)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Only a ringing call can be cancelled.");
        }

        var (ended, changed) = await TryEndAsync(session, CallEndReason.CancelledByCaller, cancellationToken)
            .ConfigureAwait(false);
        var dto = CallSessionDto.FromDomain(ended);
        if (changed)
        {
            await broadcaster.NotifyCancelledAsync(ended.ParticipantIds, dto, cancellationToken).ConfigureAwait(false);
            await broadcaster.NotifyEndedAsync(ended.ParticipantIds, dto, cancellationToken).ConfigureAwait(false);
        }

        return dto;
    }

    public async Task<CallSessionDto> LeaveAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);

        if (session.Status == CallStatus.Ringing)
        {
            return callerId == session.CallerId
                ? await CancelAsync(callId, callerId, cancellationToken).ConfigureAwait(false)
                : await DeclineAsync(callId, callerId, cancellationToken).ConfigureAwait(false);
        }

        if (session.Status == CallStatus.Ended)
        {
            return CallSessionDto.FromDomain(session);
        }

        await broadcaster.NotifyParticipantLeftAsync(session.ParticipantIds, session.Id, callerId, cancellationToken).ConfigureAwait(false);
        var (ended, changed) = await TryEndAsync(session, CallEndReason.Completed, cancellationToken)
            .ConfigureAwait(false);
        var dto = CallSessionDto.FromDomain(ended);
        if (changed)
        {
            await broadcaster.NotifyEndedAsync(ended.ParticipantIds, dto, cancellationToken).ConfigureAwait(false);
        }

        return dto;
    }

    public async Task ProcessExpiredRingingAsync(CallSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var current = await sessions.GetAsync(session.Id, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            await sessions.RemoveFromExpiryIndexAsync(session.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (current.Status != CallStatus.Ringing)
        {
            await sessions.RemoveFromExpiryIndexAsync(current.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (current.RingExpiresAtUtc > now)
        {
            return;
        }

        var recipientId = current.ParticipantIds.FirstOrDefault(id => id != current.CallerId);
        var (ended, changed) = await TryEndAsync(current, CallEndReason.Missed, cancellationToken)
            .ConfigureAwait(false);
        if (!changed)
        {
            return;
        }

        if (recipientId != Guid.Empty)
        {
            await events.PublishAsync(
                new CallMissedV2Event(
                    ended.Id,
                    ended.ConversationId,
                    ended.CallerId,
                    recipientId,
                    ended.ParticipantIds,
                    ended.CallType,
                    now - ended.CreatedAtUtc,
                    now),
                cancellationToken).ConfigureAwait(false);
        }

        await broadcaster.NotifyEndedAsync(ended.ParticipantIds, CallSessionDto.FromDomain(ended), cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Call {CallId} expired as missed.", ended.Id);
        }
    }

    private async Task<(CallSession Session, bool Changed)> TryEndAsync(
        CallSession session,
        CallEndReason reason,
        CancellationToken cancellationToken)
    {
        if (session.Status == CallStatus.Ended)
        {
            return (session, false);
        }

        var now = timeProvider.GetUtcNow();
        var ended = session with
        {
            Status = CallStatus.Ended,
            EndedAtUtc = now,
            EndReason = reason,
        };
        var saved = await sessions.TrySaveAsync(session, ended, options.Value.EndedSessionTtl, cancellationToken)
            .ConfigureAwait(false);
        if (!saved)
        {
            var current = await RequireSessionAsync(session.Id, cancellationToken).ConfigureAwait(false);
            return (current, false);
        }

        await events.PublishAsync(
            new CallEndedV2Event(
                ended.Id,
                ended.ConversationId,
                ended.CallerId,
                ended.ParticipantIds,
                ended.CallType,
                ended.DurationUntil(now),
                reason,
                now),
            cancellationToken).ConfigureAwait(false);

        return (ended, true);
    }

    private async Task<CallSession> RequireSessionAsync(Guid callId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(callId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            throw new CallProblemException(StatusCodes.Status404NotFound, "Call was not found.");
        }

        return session;
    }

    private static void EnsureDirectParticipantConversation(
        CallConversationMetadata conversation,
        string conversationId,
        Guid callerId)
    {
        if (!conversation.Exists)
        {
            throw new CallProblemException(StatusCodes.Status404NotFound, "Conversation was not found.");
        }

        if (!string.Equals(conversation.Type, "Direct", StringComparison.OrdinalIgnoreCase))
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Calls are available only in direct chats.");
        }

        if (conversation.ParticipantIds.Count != 2 || !conversation.ParticipantIds.Contains(callerId))
        {
            throw new CallProblemException(StatusCodes.Status403Forbidden, $"User cannot start a call in conversation {conversationId}.");
        }
    }

    private static void EnsureSessionParticipant(CallSession session, Guid callerId)
    {
        if (!session.IsParticipant(callerId))
        {
            throw new CallProblemException(StatusCodes.Status403Forbidden, "User is not a call participant.");
        }
    }

    private static IReadOnlyList<Guid> AddConnected(CallSession session, Guid userId)
        => session.ConnectedParticipantIds.Contains(userId)
            ? session.ConnectedParticipantIds
            : session.ConnectedParticipantIds.Append(userId).ToList();

    private static string RoomNameFor(Guid callId) => $"call-{callId:N}";

    private string GetLiveKitServerUrl()
        => liveKitOptions.Value.ServerUrl;
}
