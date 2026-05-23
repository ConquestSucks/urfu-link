namespace UserService.Api.Domain;

// Денормализованная проекция данных Keycloak для поиска по пользователям.
// Источник истины — Keycloak; запись сюда наполняется двумя независимыми каналами:
//   1. UserSearchReconciler — периодический пейджинг /admin/realms/.../users.
//   2. UserSearchLazyUpserter — на лету при первом запросе авторизованного юзера
//      (берёт данные прямо из JWT-клеймов: sub/name/email/preferred_username).
// Никаких domain-событий: проекция read-only с точки зрения бизнес-логики UserProfile.
public sealed class UserSearchProjection
{
    public Guid UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Email { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    // Нормализованный конкатенат (lower + unaccent) для GIN trgm. В SQL-запросе индекс
    // pg_trgm даст и prefix-match («ива» → Иванов), и fuzzy-match через similarity().
    public string SearchText { get; private set; } = string.Empty;

    // Транслитерированная пара: оригинал + противоположный алфавит. Поддерживает
    // переключение раскладки в обе стороны («kirill» → найдёт «Кирилл», и наоборот).
    public string SearchTextTranslit { get; private set; } = string.Empty;

    // KC users.modifiedTimestamp — монотонный source-of-truth для идемпотентного UPSERT.
    // 0 для lazy-upsert из JWT (claim не несёт modifiedTimestamp; перезатрётся reconciler-ом).
    public long KeycloakModifiedMs { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private UserSearchProjection() { }

    public static UserSearchProjection Create(
        Guid userId,
        string username,
        string? firstName,
        string? lastName,
        string? email,
        string displayName,
        string searchText,
        string searchTextTranslit,
        long keycloakModifiedMs)
    {
        return new UserSearchProjection
        {
            UserId = userId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            DisplayName = displayName,
            SearchText = searchText,
            SearchTextTranslit = searchTextTranslit,
            KeycloakModifiedMs = keycloakModifiedMs,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
