using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed class GetReadReceiptsQuery(
    IConversationRepository conversations,
    IMessageRepository messages)
{
    public async Task<IReadOnlyList<ReadReceiptDto>> ExecuteAsync(
        Guid messageId,
        Guid callerUserId,
        CancellationToken cancellationToken)
    {
        var message = await messages.GetByIdAsync(messageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatMessageNotFoundException.For(messageId);

        var conversation = await conversations.GetByIdAsync(message.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(message.ConversationId);

        if (!conversation.IsParticipant(callerUserId))
        {
            throw new ChatAccessDeniedException(message.ConversationId, callerUserId);
        }

        var receipts = await messages.GetReadReceiptsAsync(messageId, cancellationToken).ConfigureAwait(false);
        return receipts.Select(ReadReceiptDto.FromDomain).ToList();
    }
}
