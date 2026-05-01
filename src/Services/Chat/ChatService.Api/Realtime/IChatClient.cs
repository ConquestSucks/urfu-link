using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Realtime;

/// <summary>
/// Server-to-client method surface broadcast by <see cref="ChatHub"/>.
/// </summary>
public interface IChatClient
{
    Task ConversationUpdated(ConversationDto conversation);

    /// <summary>
    /// Delivered to a user when a new conversation appears in their list. For discipline groups
    /// this fires for the owner Teacher on DisciplineCreated and for every newly enrolled user
    /// on UserEnrolled.
    /// </summary>
    Task ConversationCreated(ConversationDto conversation);

    /// <summary>
    /// Delivered to existing participants of a group when a new participant joins.
    /// </summary>
    Task ParticipantJoined(string conversationId, Guid userId, ParticipantRole role);

    /// <summary>
    /// Delivered to remaining participants (and the unenrolled user themselves on their other
    /// open connections) when a user is removed from a group.
    /// </summary>
    Task ParticipantLeft(string conversationId, Guid userId);

    /// <summary>
    /// Delivered to participants when a role transition happens — e.g. a Student is promoted
    /// to Teacher inside a discipline group.
    /// </summary>
    Task ParticipantRoleChanged(string conversationId, Guid userId, ParticipantRole newRole);

    /// <summary>
    /// Delivered to participants when a conversation transitions to read-only archived state
    /// (currently only DisciplineDeleted).
    /// </summary>
    Task ConversationArchived(string conversationId, DateTimeOffset archivedAtUtc);

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

    /// <summary>
    /// Delivered to current thread subscribers when a new user joins (manually or by reply).
    /// Reason indicates why the user was added so clients can render different UI cues.
    /// </summary>
    Task ThreadParticipantJoined(Guid rootMessageId, Guid userId, string reason);
}
