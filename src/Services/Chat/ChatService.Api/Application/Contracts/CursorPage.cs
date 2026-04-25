namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
