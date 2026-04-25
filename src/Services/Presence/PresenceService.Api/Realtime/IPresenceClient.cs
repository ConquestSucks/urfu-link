using Urfu.Link.Services.Presence.Domain.Enums;

namespace Urfu.Link.Services.Presence.Realtime;

public interface IPresenceClient
{
    Task UserPresenceChanged(
        Guid userId,
        PresenceStatus status,
        Platform[] platforms,
        DateTimeOffset? lastSeenAt);

    Task UserTyping(Guid conversationId, Guid userId, bool isTyping);
}
