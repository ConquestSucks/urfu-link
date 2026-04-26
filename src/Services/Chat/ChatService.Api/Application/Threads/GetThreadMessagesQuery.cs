using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Threads;

/// <summary>
/// Cursor-paginated reply list for a thread. Authorization mirrors the main flow: only
/// participants of the parent conversation may read the thread, regardless of subscription
/// state. The cursor type and limit clamps match
/// <see cref="Messages.GetConversationMessagesQuery"/> so client cursor logic is uniform.
/// </summary>
public sealed class GetThreadMessagesQuery(
    IConversationRepository conversations,
    IMessageRepository messages)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public async Task<CursorPage<MessageDto>> ExecuteAsync(
        Guid rootMessageId,
        Guid callerUserId,
        string? cursor,
        int? limit,
        CursorDirection direction,
        CancellationToken cancellationToken)
    {
        var root = await messages.GetByIdAsync(rootMessageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatThreadRootNotFoundException.For(rootMessageId);

        if (root.IsThreadReply)
        {
            throw ChatThreadCannotReplyToReplyException.For(root.Id);
        }

        if (root.State == MessageState.Deleted)
        {
            throw ChatThreadRootNotFoundException.For(rootMessageId);
        }

        var conversation = await conversations.GetByIdAsync(root.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(root.ConversationId);

        if (!conversation.IsParticipant(callerUserId))
        {
            throw new ChatAccessDeniedException(conversation.Id, callerUserId);
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var decodedCursor = CursorCodec.DecodeMessage(cursor);

        // limit + 1 lets us know whether more pages exist without an extra round trip.
        var fetched = await messages.ListThreadAsync(
            rootMessageId, decodedCursor, effectiveLimit + 1, direction, cancellationToken).ConfigureAwait(false);

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
