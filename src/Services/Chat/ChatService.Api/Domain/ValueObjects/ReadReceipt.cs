namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record ReadReceipt(
    Guid UserId,
    DateTimeOffset ReadAtUtc);
