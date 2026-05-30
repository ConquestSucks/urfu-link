using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Call.Application;
using Urfu.Link.Services.Call.Application.Calls;
using Urfu.Link.Services.Call.Application.Chat;
using Urfu.Link.Services.Call.Application.Contracts;
using Urfu.Link.Services.Call.Domain;
using Urfu.Link.Services.Call.Realtime;

namespace CallService.UnitTests;

public sealed class CallSessionServiceTests
{
    private const string ConversationId = "direct-1";

    private static readonly Guid CallerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CalleeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset StartTime = new(2026, 5, 30, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeChatConversationClient _chat = new();
    private readonly InMemoryCallSessionStore _store = new();
    private readonly CapturingCallBroadcaster _broadcaster = new();
    private readonly CapturingOutboxWriter _outbox = new();
    private readonly ManualTimeProvider _time = new(StartTime);
    private readonly CallSessionService _service;

    public CallSessionServiceTests()
    {
        _chat.Set(ConversationId, DirectConversation(CallerId, CalleeId));

        var callOptions = Options.Create(new CallOptions
        {
            RingTimeout = TimeSpan.FromSeconds(30),
            SessionTtl = TimeSpan.FromHours(2),
            EndedSessionTtl = TimeSpan.FromHours(1),
        });
        var liveKitOptions = Options.Create(new LiveKitOptions
        {
            ServerUrl = "wss://livekit.test",
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            TokenTtl = TimeSpan.FromMinutes(10),
        });
        var tokenProvider = new LiveKitTokenProvider(liveKitOptions, _time);
        var dispatcher = new CallEventDispatcher(
            _outbox,
            new ServiceProfile("call-service", "signaling", KafkaTopicNames.CallEvents, "call.sample.v1"));

        _service = new CallSessionService(
            _chat,
            _store,
            tokenProvider,
            callOptions,
            liveKitOptions,
            _time,
            _broadcaster,
            dispatcher,
            NullLogger<CallSessionService>.Instance);
    }

    [Fact]
    public async Task StartAsync_creates_ringing_direct_call_and_notifies_callee()
    {
        var call = await _service.StartAsync(ConversationId, CallerId, CallType.Audio, CancellationToken.None);

        call.Status.Should().Be(CallStatus.Ringing);
        call.ConversationId.Should().Be(ConversationId);
        call.CallerId.Should().Be(CallerId);
        call.ParticipantIds.Should().BeEquivalentTo([CallerId, CalleeId]);
        call.Participants.Single(p => p.UserId == CallerId).IsConnected.Should().BeTrue();
        call.Participants.Single(p => p.UserId == CalleeId).IsConnected.Should().BeFalse();
        call.RingExpiresAtUtc.Should().Be(StartTime.AddSeconds(30));

        _outbox.Events.OfType<CallIncomingV2Event>().Should().ContainSingle()
            .Which.CallId.Should().Be(call.Id);
        _broadcaster.Incoming.Should().ContainSingle();
        _broadcaster.Incoming.Single().Recipients.Should().Equal(CalleeId);
    }

    [Fact]
    public async Task StartAsync_returns_404_for_missing_conversation()
    {
        _chat.Set(ConversationId, new CallConversationMetadata(false, "Direct", []));

        var act = () => _service.StartAsync(ConversationId, CallerId, CallType.Audio, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task StartAsync_rejects_non_direct_conversation()
    {
        _chat.Set(ConversationId, new CallConversationMetadata(true, "Group", [CallerId, CalleeId]));

        var act = () => _service.StartAsync(ConversationId, CallerId, CallType.Video, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task StartAsync_rejects_non_participant()
    {
        _chat.Set(ConversationId, DirectConversation(CalleeId, OtherUserId));

        var act = () => _service.StartAsync(ConversationId, CallerId, CallType.Audio, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AcceptAsync_by_callee_activates_call_and_broadcasts_join()
    {
        var ringing = await StartCallAsync();

        var active = await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);

        active.Status.Should().Be(CallStatus.Active);
        active.AcceptedAtUtc.Should().Be(StartTime);
        active.Participants.Where(p => p.IsConnected).Select(p => p.UserId)
            .Should().BeEquivalentTo([CallerId, CalleeId]);
        _broadcaster.Accepted.Should().ContainSingle();
        _broadcaster.Joined.Should().ContainSingle(j => j.CallId == ringing.Id && j.UserId == CalleeId);
    }

    [Fact]
    public async Task AcceptAsync_rejects_caller_for_own_ringing_call()
    {
        var ringing = await StartCallAsync();

        var act = () => _service.AcceptAsync(ringing.Id, CallerId, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task AcceptAsync_is_idempotent_for_active_call()
    {
        var ringing = await StartCallAsync();
        await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);
        _broadcaster.Clear();

        var active = await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);

        active.Status.Should().Be(CallStatus.Active);
        _broadcaster.Accepted.Should().BeEmpty();
        _broadcaster.Joined.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptAsync_is_idempotent_for_ended_call()
    {
        var ringing = await StartCallAsync();
        await _service.CancelAsync(ringing.Id, CallerId, CancellationToken.None);
        _broadcaster.Clear();

        var ended = await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);

        ended.Status.Should().Be(CallStatus.Ended);
        _broadcaster.Accepted.Should().BeEmpty();
        _broadcaster.Joined.Should().BeEmpty();
    }

    [Fact]
    public async Task DeclineAsync_by_callee_ends_call()
    {
        var ringing = await StartCallAsync();

        var ended = await _service.DeclineAsync(ringing.Id, CalleeId, CancellationToken.None);

        ended.Status.Should().Be(CallStatus.Ended);
        ended.EndReason.Should().Be(CallEndReason.DeclinedByCallee);
        _outbox.Events.OfType<CallEndedV2Event>().Should().ContainSingle()
            .Which.Reason.Should().Be(CallEndReason.DeclinedByCallee);
        _broadcaster.Declined.Should().ContainSingle();
        _broadcaster.Ended.Should().ContainSingle();
    }

    [Fact]
    public async Task DeclineAsync_rejects_caller()
    {
        var ringing = await StartCallAsync();

        var act = () => _service.DeclineAsync(ringing.Id, CallerId, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task DeclineAsync_rejects_active_call()
    {
        var ringing = await StartCallAsync();
        await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);

        var act = () => _service.DeclineAsync(ringing.Id, CalleeId, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CancelAsync_allows_only_caller()
    {
        var ringing = await StartCallAsync();

        var act = () => _service.CancelAsync(ringing.Id, CalleeId, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CancelAsync_by_caller_ends_call()
    {
        var ringing = await StartCallAsync();

        var ended = await _service.CancelAsync(ringing.Id, CallerId, CancellationToken.None);

        ended.Status.Should().Be(CallStatus.Ended);
        ended.EndReason.Should().Be(CallEndReason.CancelledByCaller);
        _broadcaster.Cancelled.Should().ContainSingle();
        _broadcaster.Ended.Should().ContainSingle();
    }

    [Fact]
    public async Task CancelAsync_rejects_active_call()
    {
        var ringing = await StartCallAsync();
        await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);

        var act = () => _service.CancelAsync(ringing.Id, CallerId, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task LeaveAsync_during_ringing_maps_caller_to_cancel()
    {
        var ringing = await StartCallAsync();

        var ended = await _service.LeaveAsync(ringing.Id, CallerId, CancellationToken.None);

        ended.EndReason.Should().Be(CallEndReason.CancelledByCaller);
        _broadcaster.Cancelled.Should().ContainSingle();
    }

    [Fact]
    public async Task LeaveAsync_during_ringing_maps_callee_to_decline()
    {
        var ringing = await StartCallAsync();

        var ended = await _service.LeaveAsync(ringing.Id, CalleeId, CancellationToken.None);

        ended.EndReason.Should().Be(CallEndReason.DeclinedByCallee);
        _broadcaster.Declined.Should().ContainSingle();
    }

    [Fact]
    public async Task LeaveAsync_during_active_call_marks_completed_and_broadcasts_left()
    {
        var ringing = await StartCallAsync();
        await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);
        _broadcaster.Clear();
        _time.Advance(TimeSpan.FromMinutes(3));

        var ended = await _service.LeaveAsync(ringing.Id, CalleeId, CancellationToken.None);

        ended.Status.Should().Be(CallStatus.Ended);
        ended.EndReason.Should().Be(CallEndReason.Completed);
        _outbox.Events.OfType<CallEndedV2Event>().Last().Duration.Should().Be(TimeSpan.FromMinutes(3));
        _broadcaster.Left.Should().ContainSingle(l => l.CallId == ringing.Id && l.UserId == CalleeId);
        _broadcaster.Ended.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateTokenAsync_returns_livekit_token_for_active_call()
    {
        var ringing = await StartCallAsync();
        await _service.AcceptAsync(ringing.Id, CalleeId, CancellationToken.None);

        var token = await _service.CreateTokenAsync(ringing.Id, CalleeId, CancellationToken.None);

        token.CallId.Should().Be(ringing.Id);
        token.ServerUrl.Should().Be("wss://livekit.test");
        token.RoomName.Should().Be($"call-{ringing.Id:N}");
        token.Token.Should().NotBeNullOrWhiteSpace();
        token.ExpiresAtUtc.Should().Be(StartTime.AddMinutes(10));
    }

    [Fact]
    public async Task CreateTokenAsync_rejects_ended_call()
    {
        var ringing = await StartCallAsync();
        await _service.CancelAsync(ringing.Id, CallerId, CancellationToken.None);

        var act = () => _service.CreateTokenAsync(ringing.Id, CalleeId, CancellationToken.None);

        await act.Should().ThrowAsync<CallProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ProcessExpiredRingingAsync_creates_missed_and_ended_events()
    {
        var ringing = await StartCallAsync();
        var session = _store.Require(ringing.Id);
        _outbox.Clear();
        _time.Advance(TimeSpan.FromSeconds(31));

        await _service.ProcessExpiredRingingAsync(session, CancellationToken.None);

        _store.Require(ringing.Id).Status.Should().Be(CallStatus.Ended);
        _store.Require(ringing.Id).EndReason.Should().Be(CallEndReason.Missed);
        _outbox.Events.OfType<CallMissedV2Event>().Should().ContainSingle()
            .Which.RecipientId.Should().Be(CalleeId);
        _outbox.Events.OfType<CallEndedV2Event>().Should().ContainSingle()
            .Which.Reason.Should().Be(CallEndReason.Missed);
        _broadcaster.Ended.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessExpiredRingingAsync_noops_for_non_expired_call()
    {
        var ringing = await StartCallAsync();
        var session = _store.Require(ringing.Id);
        _outbox.Clear();

        await _service.ProcessExpiredRingingAsync(session, CancellationToken.None);

        _store.Require(ringing.Id).Status.Should().Be(CallStatus.Ringing);
        _outbox.Events.Should().BeEmpty();
        _broadcaster.Ended.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessExpiredRingingAsync_does_not_overwrite_call_accepted_after_expiry_scan()
    {
        var ringing = await StartCallAsync();
        var staleRingingSession = _store.Require(ringing.Id);
        var accepted = staleRingingSession with
        {
            Status = CallStatus.Active,
            AcceptedAtUtc = StartTime,
            ConnectedParticipantIds = [CallerId, CalleeId],
        };
        await _store.SaveAsync(accepted, TimeSpan.FromHours(2), CancellationToken.None);
        _outbox.Clear();
        _broadcaster.Clear();
        _time.Advance(TimeSpan.FromSeconds(31));

        await _service.ProcessExpiredRingingAsync(staleRingingSession, CancellationToken.None);

        _store.Require(ringing.Id).Status.Should().Be(CallStatus.Active);
        _store.Require(ringing.Id).EndReason.Should().BeNull();
        _outbox.Events.Should().BeEmpty();
        _broadcaster.Ended.Should().BeEmpty();
    }

    private Task<CallSessionDto> StartCallAsync()
        => _service.StartAsync(ConversationId, CallerId, CallType.Audio, CancellationToken.None);

    private static CallConversationMetadata DirectConversation(params Guid[] participantIds)
        => new(true, "Direct", participantIds);

    private sealed class FakeChatConversationClient : IChatConversationClient
    {
        private readonly Dictionary<string, CallConversationMetadata> _conversations = [];

        public void Set(string conversationId, CallConversationMetadata metadata)
            => _conversations[conversationId] = metadata;

        public Task<CallConversationMetadata> GetConversationAsync(
            string conversationId,
            CancellationToken cancellationToken)
            => Task.FromResult(_conversations.GetValueOrDefault(
                conversationId,
                new CallConversationMetadata(false, string.Empty, [])));
    }

    private sealed class InMemoryCallSessionStore : ICallSessionStore
    {
        private readonly Dictionary<Guid, CallSession> _sessions = [];

        public Task<bool> TryCreateAsync(CallSession session, TimeSpan ttl, CancellationToken cancellationToken)
        {
            if (_sessions.ContainsKey(session.Id))
            {
                return Task.FromResult(false);
            }

            _sessions[session.Id] = session;
            return Task.FromResult(true);
        }

        public Task<CallSession?> GetAsync(Guid callId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.GetValueOrDefault(callId));

        public Task SaveAsync(CallSession session, TimeSpan ttl, CancellationToken cancellationToken)
        {
            _sessions[session.Id] = session;
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync(
            CallSession expectedSession,
            CallSession session,
            TimeSpan ttl,
            CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(expectedSession.Id, out var current) || current != expectedSession)
            {
                return Task.FromResult(false);
            }

            _sessions[session.Id] = session;
            return Task.FromResult(true);
        }

        public Task RemoveFromExpiryIndexAsync(Guid callId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CallSession>> ListExpiredRingingAsync(
            DateTimeOffset nowUtc,
            int limit,
            CancellationToken cancellationToken)
        {
            var expired = _sessions.Values
                .Where(session => session.Status == CallStatus.Ringing && session.RingExpiresAtUtc <= nowUtc)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<CallSession>>(expired);
        }

        public CallSession Require(Guid callId) => _sessions[callId];
    }

    private sealed class CapturingCallBroadcaster : ICallBroadcaster
    {
        public List<(IReadOnlyList<Guid> Recipients, CallSessionDto Call)> Incoming { get; } = [];

        public List<(IReadOnlyList<Guid> Recipients, CallSessionDto Call)> Accepted { get; } = [];

        public List<(IReadOnlyList<Guid> Recipients, CallSessionDto Call)> Declined { get; } = [];

        public List<(IReadOnlyList<Guid> Recipients, CallSessionDto Call)> Cancelled { get; } = [];

        public List<(IReadOnlyList<Guid> Recipients, Guid CallId, Guid UserId)> Joined { get; } = [];

        public List<(IReadOnlyList<Guid> Recipients, Guid CallId, Guid UserId)> Left { get; } = [];

        public List<(IReadOnlyList<Guid> Recipients, CallSessionDto Call)> Ended { get; } = [];

        public Task NotifyIncomingAsync(
            IReadOnlyList<Guid> recipientUserIds,
            CallSessionDto call,
            CancellationToken cancellationToken)
        {
            Incoming.Add((recipientUserIds, call));
            return Task.CompletedTask;
        }

        public Task NotifyAcceptedAsync(
            IReadOnlyList<Guid> participantUserIds,
            CallSessionDto call,
            CancellationToken cancellationToken)
        {
            Accepted.Add((participantUserIds, call));
            return Task.CompletedTask;
        }

        public Task NotifyDeclinedAsync(
            IReadOnlyList<Guid> participantUserIds,
            CallSessionDto call,
            CancellationToken cancellationToken)
        {
            Declined.Add((participantUserIds, call));
            return Task.CompletedTask;
        }

        public Task NotifyCancelledAsync(
            IReadOnlyList<Guid> participantUserIds,
            CallSessionDto call,
            CancellationToken cancellationToken)
        {
            Cancelled.Add((participantUserIds, call));
            return Task.CompletedTask;
        }

        public Task NotifyParticipantJoinedAsync(
            IReadOnlyList<Guid> participantUserIds,
            Guid callId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            Joined.Add((participantUserIds, callId, userId));
            return Task.CompletedTask;
        }

        public Task NotifyParticipantLeftAsync(
            IReadOnlyList<Guid> participantUserIds,
            Guid callId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            Left.Add((participantUserIds, callId, userId));
            return Task.CompletedTask;
        }

        public Task NotifyEndedAsync(
            IReadOnlyList<Guid> participantUserIds,
            CallSessionDto call,
            CancellationToken cancellationToken)
        {
            Ended.Add((participantUserIds, call));
            return Task.CompletedTask;
        }

        public void Clear()
        {
            Incoming.Clear();
            Accepted.Clear();
            Declined.Clear();
            Cancelled.Clear();
            Joined.Clear();
            Left.Clear();
            Ended.Clear();
        }
    }

    private sealed class CapturingOutboxWriter : IOutboxWriter
    {
        public List<IIntegrationEvent> Events { get; } = [];

        public ValueTask EnqueueAsync<TEvent>(
            string topic,
            IntegrationEnvelope<TEvent> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Events.Add(envelope.Payload);
            return ValueTask.CompletedTask;
        }

        public void Clear() => Events.Clear();
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
