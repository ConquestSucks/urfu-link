namespace UserService.Api.Domain.Interfaces;

// Источник истины по «контактной» части профиля (имя, email) — Keycloak.
// Аватары хранятся в UserService (Account.AvatarUrl) и должны добавляться
// потребителем поверх UserDirectoryEntry, если нужны.
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserDirectoryEntry>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}

public sealed record UserDirectoryEntry(
    Guid UserId,
    string DisplayName,
    string Email);
