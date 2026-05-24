using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Contracts;

/// <summary>
/// One hit returned by full-text search: a thin projection of the message plus the search-time
/// relevance score and an optional substring snippet around the matched term.
/// </summary>
public sealed record MessageSearchResultDto(
    Guid MessageId,
    string ConversationId,
    ConversationPreviewDto ConversationPreview,
    Guid SenderId,
    string Body,
    double Score,
    DateTimeOffset CreatedAtUtc,
    string? HighlightedSnippet);

/// <summary>
/// Minimum identifying info about the conversation a hit lives in. For direct chats the
/// caller's counterparty is surfaced as <see cref="PeerUserId"/>; group chats use
/// <see cref="Title"/> once a title field is added to the aggregate. <see cref="Type"/> lets
/// the client decide which one to render.
///
/// <see cref="AvatarUrl"/> and <see cref="SenderName"/> are optional UI niceties: when
/// the search service can cheaply resolve them (e.g. from a participant snapshot or a
/// UserService lookup), it sets them so the client can show a recognisable card without
/// a follow-up round trip. They are nullable because the backend may not always have
/// them — the client renders avatar initials and falls back to the title in that case.
/// </summary>
public sealed record ConversationPreviewDto(
    ConversationType Type,
    string? Title,
    Guid? PeerUserId,
    string? AvatarUrl = null,
    string? SenderName = null);
