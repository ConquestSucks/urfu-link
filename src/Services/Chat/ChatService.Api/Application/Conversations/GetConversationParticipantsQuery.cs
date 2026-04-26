using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed record ConversationParticipantDto(Guid UserId, ParticipantRole Role);

public sealed class GetConversationParticipantsQuery(IConversationRepository repository)
{
    public async Task<IReadOnlyList<ConversationParticipantDto>> ExecuteAsync(
        string conversationId,
        Guid callerUserId,
        bool callerIsAdmin,
        CancellationToken cancellationToken)
    {
        var conversation = await repository.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(conversationId);

        if (!conversation.IsParticipant(callerUserId) && !callerIsAdmin)
        {
            throw new ChatAccessDeniedException(conversationId, callerUserId);
        }

        // Project the merged list of participants and their roles. Direct chats — and any
        // legacy doc that pre-dates role tracking — surface ParticipantRole.Member for every
        // user via Conversation.RoleOf.
        return conversation.Participants
            .Select(userId => new ConversationParticipantDto(userId, conversation.RoleOf(userId)))
            .ToList();
    }
}
