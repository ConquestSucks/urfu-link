namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record EditHistoryEntry(
    string Body,
    DateTimeOffset EditedAtUtc);
