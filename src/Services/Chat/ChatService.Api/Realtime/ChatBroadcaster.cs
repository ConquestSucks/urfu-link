using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Chat.Application.Contracts;

namespace Urfu.Link.Services.Chat.Realtime;

/// <summary>
/// Sends realtime updates over SignalR. Uses the non-typed <see cref="IHubContext{THub}"/> +
/// <c>SendAsync(method, args)</c> form because the typed-client open generic
/// <c>IHubContext&lt;THub, TClient&gt;</c> isn't reliably resolvable under strict DI
/// validation in the test factory; the method names are kept aligned with
/// <see cref="IChatClient"/>.
/// </summary>
internal sealed class ChatBroadcaster(IHubContext<ChatHub> hub) : IChatBroadcaster
{
    public Task NotifyConversationUpdatedAsync(
        IReadOnlyList<Guid> participantUserIds,
        ConversationDto conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        return hub.Clients.Users(ToUserIds(participantUserIds))
            .SendAsync(nameof(IChatClient.ConversationUpdated), conversation, cancellationToken);
    }

    public Task NotifyMessageReceivedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        MessageDto message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .SendAsync(nameof(IChatClient.MessageReceived), message, cancellationToken);
    }

    public Task NotifyMessageDeliveredAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        IReadOnlyList<Guid> messageIds,
        Guid recipientUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .SendAsync(
                nameof(IChatClient.MessageDeliveredUpdate),
                conversationId,
                messageIds,
                recipientUserId,
                cancellationToken);
    }

    public Task NotifyMessageReadAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid upToMessageId,
        Guid readerUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .SendAsync(
                nameof(IChatClient.MessageReadUpdate),
                conversationId,
                upToMessageId,
                readerUserId,
                cancellationToken);
    }

    private static List<string> ToUserIds(IReadOnlyList<Guid> userIds)
        => userIds.Select(u => u.ToString("D", CultureInfo.InvariantCulture)).ToList();
}
