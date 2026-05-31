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
    ICallUserActivityStore activity,
    LiveKitTokenProvider tokenProvider,
    IOptions<CallOptions> options,
    IOptions<LiveKitOptions> liveKitOptions,
    TimeProvider timeProvider,
    ICallBroadcaster broadcaster,
    CallEventDispatcher events,
    ILogger<CallSessionService> logger)
{
    private const string DirectConversationType = "Direct";
    private const string GroupConversationType = "Group";
    private const string DisciplineGroupSubtype = "Discipline";
    private const string TeacherRole = "Teacher";

    public async Task<CallSessionDto> StartAsync(
        string conversationId,
        Guid callerId,
        CallType callType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        var conversation = await chat.GetConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var startsAsActiveGroup = EnsureCallableConversation(conversation, conversationId, callerId);

        var now = timeProvider.GetUtcNow();
        var callId = Guid.NewGuid();
        await ReserveActiveCallAsync(callerId, callId, cancellationToken).ConfigureAwait(false);
        var session = new CallSession(
            callId,
            conversationId,
            callerId,
            conversation.ParticipantIds,
            callType,
            startsAsActiveGroup ? CallStatus.Active : CallStatus.Ringing,
            now,
            now.Add(options.Value.RingTimeout),
            AcceptedAtUtc: startsAsActiveGroup ? now : null,
            EndedAtUtc: null,
            EndReason: null,
            ConnectedParticipantIds: [callerId],
            ConversationType: conversation.Type);

        var created = await sessions.TryCreateAsync(session, options.Value.SessionTtl, cancellationToken)
            .ConfigureAwait(false);
        if (!created)
        {
            await activity.ReleaseActiveCallAsync(callerId, callId, cancellationToken).ConfigureAwait(false);
            throw new CallProblemException(StatusCodes.Status409Conflict, "Call could not be created.");
        }

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
        if (!startsAsActiveGroup)
        {
            var recipients = session.ParticipantIds.Where(id => id != callerId).ToList();
            await broadcaster.NotifyIncomingAsync(recipients, dto, cancellationToken).ConfigureAwait(false);
        }

        return dto;
    }

    public async Task<CallSessionDto> GetAsync(Guid callId, Guid callerId, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        return CallSessionDto.FromDomain(session);
    }

    public async Task<CallTokenDto> CreateTokenAsync(
        Guid callId,
        Guid callerId,
        string? clientInstanceId,
        CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
        EnsureSessionParticipant(session, callerId);
        if (session.Status == CallStatus.Ended)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Call has already ended.");
        }

        await ReserveActiveCallAsync(callerId, session.Id, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(clientInstanceId))
        {
            var previousClientInstanceId = await activity.SetClientInstanceAsync(
                    session.Id,
                    callerId,
                    clientInstanceId,
                    options.Value.SessionTtl,
                    cancellationToken)
                .ConfigureAwait(false);

            if (previousClientInstanceId is not null
                && !string.Equals(previousClientInstanceId, clientInstanceId, StringComparison.Ordinal))
            {
                await broadcaster.NotifyDeviceReplacedAsync(
                        callerId,
                        session.Id,
                        clientInstanceId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
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
            if (session.ConnectedParticipantIds.Contains(callerId))
            {
                return CallSessionDto.FromDomain(session);
            }

            await ReserveActiveCallAsync(callerId, session.Id, cancellationToken).ConfigureAwait(false);
            var joined = session with
            {
                ConnectedParticipantIds = AddConnected(session, callerId),
            };
            var activeSaved = await sessions.TrySaveAsync(session, joined, options.Value.SessionTtl, cancellationToken)
                .ConfigureAwait(false);
            if (!activeSaved)
            {
                await activity.ReleaseActiveCallAsync(callerId, session.Id, cancellationToken).ConfigureAwait(false);
                var current = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
                return CallSessionDto.FromDomain(current);
            }

            await broadcaster.NotifyParticipantJoinedAsync(
                    joined.ParticipantIds,
                    joined.Id,
                    callerId,
                    cancellationToken)
                .ConfigureAwait(false);
            return CallSessionDto.FromDomain(joined);
        }

        if (callerId == session.CallerId)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Caller cannot accept their own ringing call.");
        }

        await ReserveActiveCallAsync(callerId, session.Id, cancellationToken).ConfigureAwait(false);
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
            await activity.ReleaseActiveCallAsync(callerId, session.Id, cancellationToken).ConfigureAwait(false);
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
            if (session.Status == CallStatus.Active
                && session.IsGroup
                && !session.ConnectedParticipantIds.Contains(callerId))
            {
                return CallSessionDto.FromDomain(session);
            }

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

        if (session.IsGroup && callerId != session.CallerId)
        {
            await activity.ReleaseActiveCallAsync(callerId, session.Id, cancellationToken).ConfigureAwait(false);
            if (!session.ConnectedParticipantIds.Contains(callerId))
            {
                return CallSessionDto.FromDomain(session);
            }

            var disconnected = session with
            {
                ConnectedParticipantIds = RemoveConnected(session, callerId),
            };
            var saved = await sessions.TrySaveAsync(session, disconnected, options.Value.SessionTtl, cancellationToken)
                .ConfigureAwait(false);
            if (!saved)
            {
                var current = await RequireSessionAsync(callId, cancellationToken).ConfigureAwait(false);
                return CallSessionDto.FromDomain(current);
            }

            await broadcaster.NotifyParticipantLeftAsync(
                    disconnected.ParticipantIds,
                    disconnected.Id,
                    callerId,
                    cancellationToken)
                .ConfigureAwait(false);
            return CallSessionDto.FromDomain(disconnected);
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

        await ReleaseActiveCallsAsync(ended, cancellationToken).ConfigureAwait(false);
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

    private static bool EnsureCallableConversation(
        CallConversationMetadata conversation,
        string conversationId,
        Guid callerId)
    {
        if (!conversation.Exists)
        {
            throw new CallProblemException(StatusCodes.Status404NotFound, "Conversation was not found.");
        }

        if (!conversation.ParticipantIds.Contains(callerId))
        {
            throw new CallProblemException(StatusCodes.Status403Forbidden, $"User cannot start a call in conversation {conversationId}.");
        }

        if (string.Equals(conversation.Type, DirectConversationType, StringComparison.OrdinalIgnoreCase))
        {
            if (conversation.ParticipantIds.Count != 2)
            {
                throw new CallProblemException(StatusCodes.Status403Forbidden, $"User cannot start a call in conversation {conversationId}.");
            }

            return false;
        }

        if (!string.Equals(conversation.Type, GroupConversationType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(conversation.GroupSubtype, DisciplineGroupSubtype, StringComparison.OrdinalIgnoreCase))
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "Calls are available only in direct chats and discipline group chats.");
        }

        if (!string.Equals(conversation.RoleOf(callerId), TeacherRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new CallProblemException(StatusCodes.Status403Forbidden, "Only teachers can start group calls.");
        }

        return true;
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

    private static List<Guid> RemoveConnected(CallSession session, Guid userId)
        => session.ConnectedParticipantIds.Where(id => id != userId).ToList();

    private async Task ReserveActiveCallAsync(Guid userId, Guid callId, CancellationToken cancellationToken)
    {
        var reserved = await activity.TryReserveActiveCallAsync(
                userId,
                callId,
                options.Value.SessionTtl,
                cancellationToken)
            .ConfigureAwait(false);
        if (!reserved)
        {
            throw new CallProblemException(StatusCodes.Status409Conflict, "User already has another active call.");
        }
    }

    private async Task ReleaseActiveCallsAsync(CallSession session, CancellationToken cancellationToken)
    {
        foreach (var participantId in session.ParticipantIds)
        {
            await activity.ReleaseActiveCallAsync(participantId, session.Id, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string RoomNameFor(Guid callId) => $"call-{callId:N}";

    private string GetLiveKitServerUrl()
        => liveKitOptions.Value.ServerUrl;
}
