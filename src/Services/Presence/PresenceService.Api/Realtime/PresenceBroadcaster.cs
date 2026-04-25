using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Realtime;

/// <summary>
/// Thin wrapper around <see cref="IHubContext{THub, T}"/>. Centralises the
/// "broadcast presence/typing into <c>presence:{userId}</c>" convention so
/// individual call-sites don't reinvent group naming. Note: typing events are
/// broadcast to the user's group, not to a per-conversation group, because
/// conversation membership lives in the chat service. Subscribers via
/// <c>SubscribeToUsers</c> implicitly receive typing for all conversations of
/// users they care about — this is acceptable for the MVP.
/// </summary>
public sealed class PresenceBroadcaster(IHubContext<PresenceHub, IPresenceClient> hubContext)
{
    public Task BroadcastPresenceAsync(Guid userId, AggregatedPresence presence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presence);
        return hubContext.Clients
            .Group(PresenceHub.GroupForUser(userId))
            .UserPresenceChanged(
                presence.UserId,
                presence.Status,
                presence.Platforms.ToArray(),
                presence.LastSeenAt);
    }

    public Task BroadcastTypingAsync(Guid conversationId, Guid userId, bool isTyping, CancellationToken cancellationToken)
    {
        return hubContext.Clients
            .Group(PresenceHub.GroupForUser(userId))
            .UserTyping(conversationId, userId, isTyping);
    }
}
