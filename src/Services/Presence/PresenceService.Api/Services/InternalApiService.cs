using Grpc.Core;
using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Grpc;
using DomainEnums = Urfu.Link.Services.Presence.Domain.Enums;

namespace Urfu.Link.Services.Presence.Services;

public sealed class InternalApiService(
    IPresenceSessionStore sessions,
    ITypingStore typing,
    ILastSeenRepository lastSeen,
    PresenceAggregator aggregator) : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        _ = context;
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "presence-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }

    public override async Task<PresenceInfo> GetPresence(GetPresenceRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = context;
        var userId = ParseUserId(request.UserId);
        var aggregated = await BuildAggregatedAsync(userId, context.CancellationToken).ConfigureAwait(false);
        return ToProto(aggregated);
    }

    public override async Task<GetPresenceBatchReply> GetPresenceBatch(
        GetPresenceBatchRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = context;
        var reply = new GetPresenceBatchReply();
        foreach (var raw in request.UserIds)
        {
            var userId = ParseUserId(raw);
            var aggregated = await BuildAggregatedAsync(userId, context.CancellationToken).ConfigureAwait(false);
            reply.Items.Add(ToProto(aggregated));
        }
        return reply;
    }

    public override async Task<IsOnlineReply> IsOnline(IsOnlineRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = ParseUserId(request.UserId);
        var userSessions = await sessions.GetSessionsAsync(userId, context.CancellationToken).ConfigureAwait(false);
        var aggregated = aggregator.Aggregate(userId, userSessions, lastSeenAt: null);
        return new IsOnlineReply { IsOnline = aggregated.Status != DomainEnums.PresenceStatus.Offline };
    }

    public override async Task<IsTypingReply> IsTyping(IsTypingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conv = ParseConversationId(request.ConversationId);
        var user = ParseUserId(request.UserId);
        var typingNow = await typing.IsTypingAsync(conv, user, context.CancellationToken).ConfigureAwait(false);
        return new IsTypingReply { IsTyping = typingNow };
    }

    private async Task<Domain.ValueObjects.AggregatedPresence> BuildAggregatedAsync(
        Guid userId, CancellationToken ct)
    {
        var userSessions = await sessions.GetSessionsAsync(userId, ct).ConfigureAwait(false);
        var ls = await lastSeen.GetAsync(userId, ct).ConfigureAwait(false);
        return aggregator.Aggregate(userId, userSessions, ls?.LastSeenAt);
    }

    private static PresenceInfo ToProto(Domain.ValueObjects.AggregatedPresence agg)
    {
        var info = new PresenceInfo
        {
            UserId = agg.UserId.ToString(),
            Status = (Grpc.PresenceStatus)(int)agg.Status,
            LastSeenAtUtc = agg.LastSeenAt?.ToString("O") ?? string.Empty,
        };
        foreach (var p in agg.Platforms)
        {
            info.Platforms.Add((Grpc.Platform)(int)p);
        }
        return info;
    }

    private static Guid ParseUserId(string raw)
    {
        if (!Guid.TryParse(raw, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id must be a UUID"));
        }
        return id;
    }

    private static Guid ParseConversationId(string raw)
    {
        if (!Guid.TryParse(raw, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "conversation_id must be a UUID"));
        }
        return id;
    }
}
