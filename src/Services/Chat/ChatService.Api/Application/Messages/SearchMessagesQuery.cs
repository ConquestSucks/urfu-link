using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Users;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Messages;

/// <summary>
/// Endpoint-facing parameters for <see cref="SearchMessagesQuery"/>. Keeps cursor/limit
/// concerns separate from <see cref="MessageSearchCriteria"/> (which is the repository-level
/// filter).
/// </summary>
public sealed record SearchMessagesParameters(
    string Query,
    string? ConversationId,
    Guid? SenderId,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    bool? HasAttachments,
    AttachmentType? AttachmentType,
    string? Cursor,
    int? Limit);

public sealed class SearchMessagesQuery(
    IConversationRepository conversations,
    IMessageRepository messages,
    IUserServiceClient users)
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public async Task<CursorPage<MessageSearchResultDto>> ExecuteAsync(
        SearchMessagesParameters parameters,
        Guid callerUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var limit = Math.Clamp(parameters.Limit ?? DefaultLimit, 1, MaxLimit);

        // Access control is a single boundary: pull the caller's conversation set, optionally
        // restrict to the requested conversationId, and silently return empty for anything
        // outside that scope. Search MUST NOT 403 on outside-conversation queries — that would
        // leak which conversations exist.
        var allowedIds = await conversations.GetUserConversationIdsAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (allowedIds.Count == 0)
        {
            return new CursorPage<MessageSearchResultDto>(Array.Empty<MessageSearchResultDto>(), null);
        }

        IReadOnlyList<string> scope = parameters.ConversationId is { Length: > 0 } cid
            ? (allowedIds.Contains(cid) ? new[] { cid } : Array.Empty<string>())
            : allowedIds;

        if (scope.Count == 0)
        {
            return new CursorPage<MessageSearchResultDto>(Array.Empty<MessageSearchResultDto>(), null);
        }

        var cursor = CursorCodec.DecodeMessageSearch(parameters.Cursor);

        var criteria = new MessageSearchCriteria(
            parameters.Query,
            parameters.SenderId,
            parameters.DateFrom,
            parameters.DateTo,
            parameters.HasAttachments,
            parameters.AttachmentType);

        // Fetch one extra row so we can decide whether a next page exists without re-issuing the
        // query — same pattern as ListByConversationAsync.
        var hits = await messages.SearchAsync(
            criteria,
            scope,
            cursor,
            limit + 1,
            cancellationToken).ConfigureAwait(false);

        var hasMore = hits.Count > limit;
        var pageHits = hasMore ? hits.Take(limit).ToList() : hits.ToList();

        // Single batch lookup for conversation previews and author info — no N+1.
        List<string> distinctConvIds = pageHits.Select(h => h.Message.ConversationId).Distinct().ToList();
        var conversationLookup = await BuildConversationLookupAsync(distinctConvIds, cancellationToken).ConfigureAwait(false);
        var userLookup = await BuildUserLookupAsync(pageHits, conversationLookup.Values, callerUserId, cancellationToken)
            .ConfigureAwait(false);

        var items = pageHits.Select(h => new MessageSearchResultDto(
            MessageId: h.Message.Id,
            ConversationId: h.Message.ConversationId,
            ConversationPreview: conversationLookup.TryGetValue(h.Message.ConversationId, out var conversation)
                ? BuildPreview(conversation, callerUserId, h.Message.SenderId, userLookup)
                : BuildFallbackPreview(h.Message.SenderId, userLookup),
            SenderId: h.Message.SenderId,
            Body: h.Message.Body,
            Score: h.Score,
            CreatedAtUtc: h.Message.CreatedAtUtc,
            HighlightedSnippet: MessageSnippetBuilder.Build(h.Message.Body, parameters.Query))).ToList();

        string? nextCursor = null;
        if (hasMore)
        {
            var last = pageHits[^1];
            nextCursor = CursorCodec.EncodeMessageSearch(
                new MessageSearchCursor(last.Score, last.Message.CreatedAtUtc, last.Message.Id));
        }

        return new CursorPage<MessageSearchResultDto>(items, nextCursor);
    }

    private async Task<Dictionary<string, Conversation>> BuildConversationLookupAsync(
        List<string> conversationIds,
        CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
        {
            return new Dictionary<string, Conversation>(StringComparer.Ordinal);
        }

        var docs = await conversations.GetByIdsAsync(conversationIds, cancellationToken).ConfigureAwait(false);
        return docs.ToDictionary(c => c.Id, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<Guid, UserSummary>> BuildUserLookupAsync(
        IReadOnlyCollection<MessageSearchHit> hits,
        IEnumerable<Conversation> resultConversations,
        Guid callerUserId,
        CancellationToken cancellationToken)
    {
        var ids = hits.Select(h => h.Message.SenderId)
            .Concat(resultConversations
                .Where(c => c.Type == ConversationType.Direct)
                .Select(c => FindDirectPeer(c, callerUserId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        return await users.BatchGetUsersAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    private static ConversationPreviewDto BuildPreview(
        Conversation conversation,
        Guid callerUserId,
        Guid senderId,
        IReadOnlyDictionary<Guid, UserSummary> userLookup)
    {
        var senderName = DisplayNameFor(userLookup, senderId);
        var senderAvatarUrl = AvatarUrlFor(userLookup, senderId);

        if (conversation.Type == ConversationType.Direct)
        {
            // Direct: surface the counterparty so the client can render a name/avatar.
            var peer = FindDirectPeer(conversation, callerUserId);
            var title = peer is { } peerId ? DisplayNameFor(userLookup, peerId) : null;
            return new ConversationPreviewDto(
                ConversationType.Direct,
                title,
                peer,
                senderAvatarUrl,
                senderName);
        }

        // Group/discipline conversations don't have a Title field on the aggregate yet — null
        // is the correct value until that work lands. Type alone tells the client this is a
        // group hit, which is enough to render a placeholder.
        return new ConversationPreviewDto(conversation.Type, null, null, senderAvatarUrl, senderName);
    }

    private static ConversationPreviewDto BuildFallbackPreview(
        Guid senderId,
        IReadOnlyDictionary<Guid, UserSummary> userLookup)
    {
        return new ConversationPreviewDto(
            ConversationType.Direct,
            DisplayNameFor(userLookup, senderId),
            null,
            AvatarUrlFor(userLookup, senderId),
            DisplayNameFor(userLookup, senderId));
    }

    private static Guid? FindDirectPeer(Conversation conversation, Guid callerUserId)
    {
        var peer = conversation.Participants.FirstOrDefault(p => p != callerUserId);
        return peer == Guid.Empty ? null : peer;
    }

    private static string? DisplayNameFor(IReadOnlyDictionary<Guid, UserSummary> userLookup, Guid userId)
    {
        return userLookup.TryGetValue(userId, out var summary) && !string.IsNullOrWhiteSpace(summary.DisplayName)
            ? summary.DisplayName
            : null;
    }

    private static string? AvatarUrlFor(IReadOnlyDictionary<Guid, UserSummary> userLookup, Guid userId)
    {
        return userLookup.TryGetValue(userId, out var summary) && !string.IsNullOrWhiteSpace(summary.AvatarUrl)
            ? summary.AvatarUrl
            : null;
    }
}
