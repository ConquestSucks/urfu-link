namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record Reaction(
    Guid UserId,
    string Emoji,
    DateTimeOffset CreatedAtUtc);
