using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed class GetConversationQuery(IConversationRepository repository)
{
    public async Task<ConversationDto> ExecuteAsync(string conversationId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var conversation = await repository.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(conversationId);

        if (!conversation.IsParticipant(callerUserId))
        {
            throw new ChatAccessDeniedException(conversationId, callerUserId);
        }

        return ConversationDto.FromDomain(conversation);
    }
}
