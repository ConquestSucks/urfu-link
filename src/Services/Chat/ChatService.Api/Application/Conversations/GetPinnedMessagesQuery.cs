using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed class GetPinnedMessagesQuery(
    IConversationRepository conversations,
    IMessageRepository messages)
{
    public async Task<IReadOnlyList<MessageDto>> ExecuteAsync(
        string conversationId,
        Guid callerUserId,
        CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(conversationId);

        if (!conversation.IsParticipant(callerUserId))
        {
            throw new ChatAccessDeniedException(conversationId, callerUserId);
        }

        if (conversation.PinnedMessageIds.Count == 0)
        {
            return Array.Empty<MessageDto>();
        }

        var pinnedMessages = await messages.GetByIdsAsync(
            conversation.Id,
            conversation.PinnedMessageIds,
            cancellationToken).ConfigureAwait(false);

        return OrderPinnedMessages(conversation, pinnedMessages);
    }

    internal static IReadOnlyList<MessageDto> OrderPinnedMessages(
        Conversation conversation,
        IReadOnlyList<Message> pinnedMessages)
    {
        if (pinnedMessages.Count == 0)
        {
            return Array.Empty<MessageDto>();
        }

        var messagesById = pinnedMessages.ToDictionary(m => m.Id);
        return conversation.PinnedMessageIds
            .Where(messagesById.ContainsKey)
            .Select(id => MessageDto.FromDomain(messagesById[id]))
            .ToList();
    }
}
