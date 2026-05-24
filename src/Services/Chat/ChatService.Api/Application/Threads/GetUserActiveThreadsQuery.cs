using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Threads;

/// <summary>
/// Returns the user's "active threads" — every thread they are subscribed to (manually,
/// because they replied, or because they were mentioned), ordered by lastActivityAtUtc desc.
/// Tombstoned roots are filtered out at projection time so a deleted root does not surface in
/// the list even if the subscription document still exists.
/// </summary>
public sealed class GetUserActiveThreadsQuery(
    IThreadSubscriptionRepository subscriptions,
    IMessageRepository messages)
{
    private const int DefaultLimit = 30;
    private const int MaxLimit = 100;

    public async Task<CursorPage<ActiveThreadDto>> ExecuteAsync(
        Guid callerUserId,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var decodedCursor = CursorCodec.DecodeThreadActivity(cursor);

        var fetched = await subscriptions.ListUserActiveAsync(
            callerUserId, decodedCursor, effectiveLimit + 1, cancellationToken).ConfigureAwait(false);

        if (fetched.Count == 0)
        {
            return new CursorPage<ActiveThreadDto>(Array.Empty<ActiveThreadDto>(), null);
        }

        // Batch-load root messages in a single round trip and index by id for O(1) projection.
        var rootIds = fetched.Select(s => s.RootMessageId).Distinct().ToList();
        var roots = await messages.GetManyAsync(rootIds, cancellationToken).ConfigureAwait(false);
        var rootById = roots.ToDictionary(r => r.Id);

        var subsForPage = fetched.Take(effectiveLimit).ToList();
        var items = new List<ActiveThreadDto>(subsForPage.Count);
        foreach (var sub in subsForPage)
        {
            if (!rootById.TryGetValue(sub.RootMessageId, out var root))
            {
                // Subscription points at a missing message — possible if the root was hard-deleted
                // out of band. Skip silently rather than fail the whole page.
                continue;
            }
            if (root.State == MessageState.Deleted)
            {
                continue;
            }

            items.Add(new ActiveThreadDto(
                RootMessageId: sub.RootMessageId,
                ConversationId: root.ConversationId,
                RootMessage: MessageDto.FromDomain(root),
                ReplyCount: root.ThreadReplyCount,
                LastActivityAtUtc: sub.LastActivityAtUtc,
                Reason: sub.Reason));
        }

        string? nextCursor = null;
        if (fetched.Count > effectiveLimit)
        {
            var last = fetched[effectiveLimit - 1];
            nextCursor = CursorCodec.EncodeThreadActivity(
                new ThreadActivityCursor(last.LastActivityAtUtc, last.RootMessageId));
        }

        return new CursorPage<ActiveThreadDto>(items, nextCursor);
    }
}
