using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Realtime;

/// <summary>
/// Sends realtime updates over SignalR via the strongly-typed
/// <see cref="IHubContext{THub, T}"/> overload — method names and signatures are checked at
/// compile time against <see cref="IChatClient"/>, so a rename in the contract immediately
/// propagates here. Recipients are addressed by user id (resolved through
/// <see cref="ChatUserIdProvider"/>) so the same broadcast reaches every active connection a
/// user has.
/// </summary>
internal sealed class ChatBroadcaster(IHubContext<ChatHub, IChatClient> hub) : IChatBroadcaster
{
    public Task NotifyConversationUpdatedAsync(
        IReadOnlyList<Guid> participantUserIds,
        ConversationDto conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        return hub.Clients.Users(ToUserIds(participantUserIds)).ConversationUpdated(conversation);
    }

    public Task NotifyMessageReceivedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        MessageDto message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds)).MessageReceived(message);
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
            .MessageDeliveredUpdate(conversationId, messageIds, recipientUserId);
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
            .MessageReadUpdate(conversationId, upToMessageId, readerUserId);
    }

    public Task NotifyMessageReadByAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid messageId,
        Guid readerUserId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .MessageReadByUpdate(conversationId, messageId, readerUserId, readAtUtc);
    }

    public Task NotifyMessageEditedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        MessageDto message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds)).MessageEdited(message);
    }

    public Task NotifyMessageDeletedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid messageId,
        DeleteMode mode,
        Guid deletedBy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .MessageDeletedUpdate(conversationId, messageId, mode.ToWire(), deletedBy);
    }

    public Task NotifyReactionUpdatedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        Guid messageId,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> summary,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        ArgumentNullException.ThrowIfNull(summary);
        return hub.Clients.Users(ToUserIds(recipientUserIds)).ReactionUpdated(messageId, summary);
    }

    public Task NotifyPinsUpdatedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        IReadOnlyList<MessageDto> pinnedMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        ArgumentNullException.ThrowIfNull(pinnedMessages);
        return hub.Clients.Users(ToUserIds(recipientUserIds)).PinsUpdated(conversationId, pinnedMessages);
    }

    private static List<string> ToUserIds(IReadOnlyList<Guid> userIds)
        => userIds.Select(u => u.ToString("D", CultureInfo.InvariantCulture)).ToList();
}
