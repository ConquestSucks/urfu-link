using Urfu.Link.Services.Chat.Application.Contracts;

namespace Urfu.Link.Services.Chat.Realtime;

/// <summary>
/// Server-to-client method surface broadcast by <see cref="ChatHub"/>.
/// </summary>
public interface IChatClient
{
    Task ConversationUpdated(ConversationDto conversation);

    Task MessageReceived(MessageDto message);

    Task MessageDeliveredUpdate(string conversationId, IReadOnlyList<Guid> messageIds, Guid recipientUserId);

    Task MessageReadUpdate(string conversationId, Guid upToMessageId, Guid readerUserId);

    Task MessageReadByUpdate(string conversationId, Guid messageId, Guid readerUserId, DateTimeOffset readAtUtc);

    Task MessageEdited(MessageDto message);

    Task MessageDeletedUpdate(string conversationId, Guid messageId, string mode, Guid deletedBy);

    Task ReactionUpdated(Guid messageId, IReadOnlyDictionary<string, IReadOnlyList<Guid>> summary);

    Task PinsUpdated(string conversationId, IReadOnlyList<MessageDto> pinnedMessages);

    /// <summary>Delivered only to thread subscribers — the new reply does not surface in the main flow.</summary>
    Task ThreadReplyReceived(Guid rootMessageId, MessageDto reply);

    /// <summary>
    /// Delivered to every participant of the parent conversation so the main-flow UI can refresh
    /// the "N replies" marker on the root message without re-fetching it.
    /// </summary>
    Task ThreadRootUpdated(
        string conversationId,
        Guid rootMessageId,
        int replyCount,
        IReadOnlyList<Guid> participants,
        DateTimeOffset lastReplyAtUtc);
}
