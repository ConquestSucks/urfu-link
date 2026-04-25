namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record ForwardedFrom(
    Guid OriginalSenderId,
    DateTimeOffset OriginalSentAtUtc,
    string? OriginalConversationId);
