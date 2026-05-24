using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed class GetConversationQuery(
    IConversationRepository repository,
    IMessageRepository messages)
{
    public async Task<ConversationDto> ExecuteAsync(string conversationId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var conversation = await repository.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(conversationId);

        if (!conversation.IsParticipant(callerUserId))
        {
            throw new ChatAccessDeniedException(conversationId, callerUserId);
        }

        var unreadCounts = await messages.GetUnreadCountsByConversationIdsAsync(
            new[] { conversation.Id },
            callerUserId,
            cancellationToken).ConfigureAwait(false);
        var dto = ConversationDto.FromDomain(conversation)
            .WithUnreadCount(unreadCounts.TryGetValue(conversation.Id, out var unreadCount) ? unreadCount : 0);
        if (conversation.LastMessagePreview is null)
        {
            return dto;
        }

        var latestByConversation = await messages.GetLatestByConversationIdsAsync(
            new[] { conversation.Id },
            cancellationToken).ConfigureAwait(false);

        return latestByConversation.TryGetValue(conversation.Id, out var latest)
            ? dto.WithLastMessageMetadata(latest)
            : dto;
    }
}
