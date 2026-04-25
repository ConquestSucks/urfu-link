namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record MessagePreview(
    Guid SenderId,
    string Body,
    DateTimeOffset SentAtUtc,
    bool HasAttachments);
