using Urfu.Link.Services.Chat.Application.Presence;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

/// <summary>
/// No-op fallback for the typing fan-out used when <c>GrpcClients:PresenceService:Address</c>
/// is not configured (tests, on-prem profiles without presence). Lets ChatHub call
/// <c>SetTypingAsync</c> unconditionally — the chat-side authorization still runs, the
/// signal simply doesn't propagate.
/// </summary>
internal sealed class NoopPresenceServiceClient : IPresenceServiceClient
{
    public Task SetTypingAsync(string conversationId, Guid userId, bool isTyping, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
