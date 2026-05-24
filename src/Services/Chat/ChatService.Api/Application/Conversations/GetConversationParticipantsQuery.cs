using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Users;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

// DisplayName/AvatarUrl приходят из UserService (BatchGetUsers): chat-агрегат
// хранит только Guid'ы участников. Пустые строки означают, что lookup не
// удался (UserService недоступен / юзер удалён) — фронт рендерит fallback.
public sealed record ConversationParticipantDto(
    Guid UserId,
    ParticipantRole Role,
    string DisplayName,
    string AvatarUrl);

public sealed class GetConversationParticipantsQuery(
    IConversationRepository repository,
    IUserServiceClient userServiceClient)
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

        var ids = conversation.Participants.ToArray();
        var summaries = await userServiceClient
            .BatchGetUsersAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        // Project the merged list of participants and their roles. Direct chats — and any
        // legacy doc that pre-dates role tracking — surface ParticipantRole.Member for every
        // user via Conversation.RoleOf.
        return ids
            .Select(userId =>
            {
                summaries.TryGetValue(userId, out var summary);
                return new ConversationParticipantDto(
                    userId,
                    conversation.RoleOf(userId),
                    summary?.DisplayName ?? string.Empty,
                    summary?.AvatarUrl ?? string.Empty);
            })
            .ToList();
    }
}
