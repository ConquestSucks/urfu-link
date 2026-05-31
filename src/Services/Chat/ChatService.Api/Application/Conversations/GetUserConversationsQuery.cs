using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed class GetUserConversationsQuery(
    IConversationRepository repository,
    IMessageRepository messages)
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public async Task<CursorPage<ConversationDto>> ExecuteAsync(
        Guid userId,
        string? cursor,
        int? limit,
        ConversationListFilter filter,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var decodedCursor = CursorCodec.DecodeConversation(cursor);

        // Ask for one extra row so we can tell whether a next page exists.
        var fetched = await repository.ListByParticipantAsync(
            userId, decodedCursor, effectiveLimit + 1, filter, cancellationToken).ConfigureAwait(false);

        var pageConversations = fetched.Take(effectiveLimit).ToList();
        var conversationIds = pageConversations.Select(c => c.Id).ToList();
        var latestByConversation = await messages.GetLatestByConversationIdsAsync(
            pageConversations
                .Where(c => c.LastMessagePreview is not null)
                .Select(c => c.Id)
                .ToList(),
            cancellationToken).ConfigureAwait(false);
        var unreadCounts = await messages.GetUnreadCountsByConversationIdsAsync(
            conversationIds, userId, cancellationToken).ConfigureAwait(false);

        var items = pageConversations
            .Select(c =>
            {
                var dto = ConversationDto.FromDomain(c, userId)
                    .WithUnreadCount(unreadCounts.TryGetValue(c.Id, out var unreadCount) ? unreadCount : 0);
                return latestByConversation.TryGetValue(c.Id, out var latest)
                    ? dto.WithLastMessageMetadata(latest)
                    : dto;
            })
            .ToList();
        string? nextCursor = null;
        if (fetched.Count > effectiveLimit)
        {
            var last = fetched[effectiveLimit - 1];
            nextCursor = CursorCodec.EncodeConversation(new ConversationCursor(last.LastMessageAtUtc, last.Id));
        }

        return new CursorPage<ConversationDto>(items, nextCursor);
    }
}
