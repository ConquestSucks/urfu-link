using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed class GetUserConversationsQuery(IConversationRepository repository)
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

        var items = fetched.Take(effectiveLimit).Select(ConversationDto.FromDomain).ToList();
        string? nextCursor = null;
        if (fetched.Count > effectiveLimit)
        {
            var last = fetched[effectiveLimit - 1];
            nextCursor = CursorCodec.EncodeConversation(new ConversationCursor(last.LastMessageAtUtc, last.Id));
        }

        return new CursorPage<ConversationDto>(items, nextCursor);
    }
}
