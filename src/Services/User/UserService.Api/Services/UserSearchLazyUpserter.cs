using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using UserService.Api.Infrastructure.Auth;
using UserService.Api.Infrastructure.Search;

namespace UserService.Api.Services;

// Middleware: при первом запросе авторизованного юзера UPSERT-ит запись
// в проекцию user_search_projection, используя данные прямо из JWT-клеймов
// (sub/name/email/preferred_username). Это даёт мгновенное появление юзера
// в поиске после первого логина, без ожидания reconcile-цикла.
//
// In-memory кеш с TTL предотвращает повторные UPSERT-ы для одного и того же
// пользователя в рамках одной реплики. Между репликами кеш не синхронизируется,
// но повторный UPSERT при keycloak_modified_ms = 0 — это no-op в SQL
// (WHERE keycloak_modified_ms <= EXCLUDED.keycloak_modified_ms сработает,
// данные одинаковые).
public sealed class UserSearchLazyUpserter(
    IServiceProvider serviceProvider,
    IOptions<UserSearchLazyUpserterOptions> options,
    ILogger<UserSearchLazyUpserter> logger)
{
    private readonly UserSearchLazyUpserterOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _recentlySeen = new();

    public async Task EnsureAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        Guid userId;
        try
        {
            userId = principal.GetUserId();
        }
        catch (InvalidOperationException)
        {
            // JWT без sub — например, токен service-to-service. Не upsertим.
            return;
        }

        if (userId == Guid.Empty)
            return;

        var now = DateTimeOffset.UtcNow;
        if (_recentlySeen.TryGetValue(userId, out var lastSeen) && now - lastSeen < _options.CacheTtl)
            return;

        _recentlySeen[userId] = now;

        // Чтобы не блокировать запрос на синхронной БД-операции, гоняем upsert
        // в фоне. Если он упадёт — следующий запрос/reconciler догонит.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IUserSearchRepository>();
                var textBuilder = scope.ServiceProvider.GetRequiredService<UserSearchTextBuilder>();

                var username = principal.GetUsername();
                var email = principal.GetEmail();
                var displayName = principal.GetDisplayName();

                // KC передаёт given_name/family_name как отдельные claim'ы, если они
                // запрошены в scope. Если их нет — разобьём display_name по пробелу
                // как best-effort fallback.
                var firstName = principal.FindFirst("given_name")?.Value;
                var lastName = principal.FindFirst("family_name")?.Value;

                if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName)
                    && !string.IsNullOrWhiteSpace(displayName))
                {
                    var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    firstName = parts.Length > 0 ? parts[0] : null;
                    lastName = parts.Length > 1 ? parts[1] : null;
                }

                var resolvedUsername = !string.IsNullOrWhiteSpace(username)
                    ? username
                    : !string.IsNullOrWhiteSpace(email) ? email : displayName;

                if (string.IsNullOrWhiteSpace(resolvedUsername))
                    return;

                var (searchText, translit) = textBuilder.Build(resolvedUsername, firstName, lastName, email);

                await repository.UpsertAsync(
                    new UserSearchUpsert(
                        UserId: userId,
                        Username: resolvedUsername,
                        FirstName: firstName,
                        LastName: lastName,
                        Email: string.IsNullOrWhiteSpace(email) ? null : email,
                        DisplayName: string.IsNullOrWhiteSpace(displayName) ? resolvedUsername : displayName,
                        SearchText: searchText,
                        SearchTextTranslit: translit,
                        // 0 — маркер «данные из claim'ов, не из KC modifiedTimestamp».
                        // Reconciler позже перезатрёт настоящим значением.
                        KeycloakModifiedMs: 0),
                    cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // fire-and-forget; ошибка не должна мешать ответу клиенту
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogWarning(ex, "Lazy upsert for user {UserId} failed.", userId);
            }
        }, cancellationToken);

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

public sealed class UserSearchLazyUpserterOptions
{
    public const string SectionName = "UserSearch:LazyUpserter";

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);
}
