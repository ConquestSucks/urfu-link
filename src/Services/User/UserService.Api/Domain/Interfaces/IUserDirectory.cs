namespace UserService.Api.Domain.Interfaces;

// Источник истины по «контактной» части профиля (имя, email) — Keycloak.
// Аватары хранятся в UserService (Account.AvatarUrl) и должны добавляться
// потребителем поверх UserDirectoryEntry, если нужны.
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserDirectoryEntry>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    // Пейджинг для UserSearchReconciler — забирает срез пользователей из KC.
    // briefRepresentation=true: KC возвращает только базовые поля (id, username,
    // email, firstName, lastName, createdTimestamp), без attributes/credentials —
    // существенно дешевле для bulk-обхода.
    Task<IReadOnlyList<UserDirectoryPageItem>> ListPageAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record UserDirectoryEntry(
    Guid UserId,
    string DisplayName,
    string Email);

// Подробная запись для синхронизации в проекцию: содержит то, что нужно для
// построения SearchText (имя, фамилия, username, email) и idempotency-маркер.
public sealed record UserDirectoryPageItem(
    Guid UserId,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    string DisplayName,
    long ModifiedTimestampMs);
