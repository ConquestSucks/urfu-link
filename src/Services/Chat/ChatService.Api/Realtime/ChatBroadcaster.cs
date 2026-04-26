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

    public Task NotifyConversationCreatedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        ConversationDto conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        ArgumentNullException.ThrowIfNull(conversation);
        if (recipientUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(recipientUserIds)).ConversationCreated(conversation);
    }

    public Task NotifyParticipantJoinedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid userId,
        ParticipantRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        if (recipientUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .ParticipantJoined(conversationId, userId, role);
    }

    public Task NotifyParticipantLeftAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        if (recipientUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .ParticipantLeft(conversationId, userId);
    }

    public Task NotifyParticipantRoleChangedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        Guid userId,
        ParticipantRole newRole,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        if (recipientUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .ParticipantRoleChanged(conversationId, userId, newRole);
    }

    public Task NotifyConversationArchivedAsync(
        IReadOnlyList<Guid> recipientUserIds,
        string conversationId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        if (recipientUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(recipientUserIds))
            .ConversationArchived(conversationId, archivedAtUtc);
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

    public Task NotifyThreadReplyReceivedAsync(
        IReadOnlyList<Guid> subscriberUserIds,
        Guid rootMessageId,
        MessageDto reply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriberUserIds);
        ArgumentNullException.ThrowIfNull(reply);
        if (subscriberUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(subscriberUserIds)).ThreadReplyReceived(rootMessageId, reply);
    }

    public Task NotifyThreadRootUpdatedAsync(
        IReadOnlyList<Guid> participantUserIds,
        string conversationId,
        Guid rootMessageId,
        int replyCount,
        IReadOnlyList<Guid> participants,
        DateTimeOffset lastReplyAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantUserIds);
        ArgumentNullException.ThrowIfNull(participants);
        if (participantUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(participantUserIds))
            .ThreadRootUpdated(conversationId, rootMessageId, replyCount, participants, lastReplyAtUtc);
    }

    public Task NotifyThreadParticipantJoinedAsync(
        IReadOnlyList<Guid> subscriberUserIds,
        Guid rootMessageId,
        Guid joinedUserId,
        ThreadSubscriptionReason reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriberUserIds);
        if (subscriberUserIds.Count == 0)
        {
            return Task.CompletedTask;
        }
        return hub.Clients.Users(ToUserIds(subscriberUserIds))
            .ThreadParticipantJoined(rootMessageId, joinedUserId, reason.ToString());
    }

    private static List<string> ToUserIds(IReadOnlyList<Guid> userIds)
        => userIds.Select(u => u.ToString("D", CultureInfo.InvariantCulture)).ToList();
}
