using Urfu.Link.Services.Chat.Application.Contracts;

namespace Urfu.Link.Services.Chat.Realtime;

/// <summary>
/// Abstraction over SignalR broadcasting so application services stay testable without booting
/// a hub. The implementation pushes through <see cref="Microsoft.AspNetCore.SignalR.IHubContext{THub, T}"/>.
/// </summary>
public interface IChatBroadcaster
{
    Task NotifyConversationUpdatedAsync(
        IReadOnlyList<Guid> participantUserIds,
        ConversationDto conversation,
        CancellationToken cancellationToken);

    Task NotifyMessageReceivedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        MessageDto message,
        CancellationToken cancellationToken);

    Task NotifyMessageDeliveredAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        IReadOnlyList<Guid> messageIds,
        Guid recipientUserId,
        CancellationToken cancellationToken);

    Task NotifyMessageReadAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid upToMessageId,
        Guid readerUserId,
        CancellationToken cancellationToken);
}
