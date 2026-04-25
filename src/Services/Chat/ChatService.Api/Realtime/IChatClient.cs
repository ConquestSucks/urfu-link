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

    Task MessageEdited(MessageDto message);

    Task MessageDeletedUpdate(string conversationId, Guid messageId, string mode, Guid deletedBy);

    Task ReactionUpdated(Guid messageId, IReadOnlyDictionary<string, IReadOnlyList<Guid>> summary);

    Task PinsUpdated(string conversationId, IReadOnlyList<MessageDto> pinnedMessages);
}
