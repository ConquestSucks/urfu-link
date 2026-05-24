namespace Urfu.Link.Services.Chat.Application.Users;

// Тонкая обёртка над gRPC InternalApi.BatchGetUsers UserService. Domain
// получает чистый DTO без protobuf-типов. Если UserService недоступен,
// потребитель должен корректно работать с пустым словарём (fallback).
public interface IUserServiceClient
{
    Task<IReadOnlyDictionary<Guid, UserSummary>> BatchGetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

public sealed record UserSummary(
    Guid UserId,
    string DisplayName,
    string AvatarUrl,
    string Email);
