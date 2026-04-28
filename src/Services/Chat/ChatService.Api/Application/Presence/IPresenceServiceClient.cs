namespace Urfu.Link.Services.Chat.Application.Presence;

/// <summary>
/// Outbound view of PresenceService used by ChatHub to flip the typing indicator
/// after authorising the caller. Implemented in production by a thin gRPC wrapper
/// (<c>Infrastructure/Grpc/PresenceServiceClient</c>); tests replace it with a fake
/// so they don't need a live PresenceService.
/// </summary>
public interface IPresenceServiceClient
{
    /// <summary>
    /// Marks <paramref name="userId"/> as currently typing in the conversation.
    /// Idempotent: re-running while already typing just refreshes the TTL on the
    /// indicator. Network failures fail open — chat-side authorization has already
    /// passed at this point and dropping a typing indicator is preferable to
    /// raising an exception that aborts the SignalR call.
    /// </summary>
    Task SetTypingAsync(string conversationId, Guid userId, bool isTyping, CancellationToken cancellationToken);
}
