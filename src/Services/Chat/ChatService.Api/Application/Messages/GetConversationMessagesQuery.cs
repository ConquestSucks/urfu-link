using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed class GetConversationMessagesQuery(
    IConversationRepository conversations,
    IMessageRepository messages)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public async Task<CursorPage<MessageDto>> ExecuteAsync(
        string conversationId,
        Guid callerUserId,
        string? cursor,
        int? limit,
        CursorDirection direction,
        CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(conversationId);

        if (!conversation.IsParticipant(callerUserId))
        {
            throw new ChatAccessDeniedException(conversationId, callerUserId);
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var decodedCursor = CursorCodec.DecodeMessage(cursor);

        var fetched = await messages.ListByConversationAsync(
            conversationId, decodedCursor, effectiveLimit + 1, direction, cancellationToken).ConfigureAwait(false);

        var items = fetched.Take(effectiveLimit).Select(MessageDto.FromDomain).ToList();
        string? nextCursor = null;
        if (fetched.Count > effectiveLimit)
        {
            var last = fetched[effectiveLimit - 1];
            nextCursor = CursorCodec.EncodeMessage(new MessageCursor(last.CreatedAtUtc, last.Id));
        }

        return new CursorPage<MessageDto>(items, nextCursor);
    }
}
