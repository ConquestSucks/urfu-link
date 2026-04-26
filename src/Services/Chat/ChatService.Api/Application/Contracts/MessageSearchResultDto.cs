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
/// </summary>
public sealed record ConversationPreviewDto(ConversationType Type, string? Title, Guid? PeerUserId);
