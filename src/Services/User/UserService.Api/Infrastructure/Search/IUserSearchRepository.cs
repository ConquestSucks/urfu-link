namespace UserService.Api.Infrastructure.Search;

public interface IUserSearchRepository
{
    Task<IReadOnlyList<UserSearchHit>> SearchAsync(
        string query,
        Guid requesterId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    // UPSERT одной записи. Используется и lazy-upsert-ом из JWT-handler,
    // и reconciler-ом (per-row). Идемпотентность через keycloak_modified_ms:
    // если предлагаемый ms ≤ хранимого — запись не обновляется (важно для
    // конкуренции reconciler vs lazy-upsert).
    Task UpsertAsync(
        UserSearchUpsert item,
        CancellationToken cancellationToken = default);

    // Soft-delete для пользователей, отсутствующих в KC. Reconciler помечает
    // их по diff-у между текущим списком KC и нашей проекцией.
    Task<int> SoftDeleteMissingAsync(
        IReadOnlyCollection<Guid> existingInKeycloak,
        CancellationToken cancellationToken = default);

    // Достать кеш-маркер для решения «нужен ли lazy-upsert». Возвращает null,
    // если запись отсутствует или soft-deleted (lazy-upsert «оживит»).
    Task<DateTimeOffset?> GetUpdatedAtUtcAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    // Пакетный lookup для BatchGetUsers (gRPC) — отдаёт displayName/email
    // из локальной проекции, чтобы не дёргать Keycloak Admin API на каждого
    // участника чата. Возвращает только найденные id; отсутствующие в проекции
    // вызывающий фолбэчит на IUserDirectory (KC).
    Task<IReadOnlyDictionary<Guid, UserSearchSummary>> BatchGetSummariesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}

public sealed record UserSearchSummary(
    Guid UserId,
    string DisplayName,
    string Email);

public sealed record UserSearchHit(
    Guid UserId,
    string DisplayName,
    string Username);

public sealed record UserSearchUpsert(
    Guid UserId,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    string DisplayName,
    string SearchText,
    string SearchTextTranslit,
    long KeycloakModifiedMs);
