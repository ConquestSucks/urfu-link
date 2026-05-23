using FastEndpoints;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;
using UserService.Api.Infrastructure.Search;

namespace UserService.Api.Endpoints;

public sealed class SearchUsersEndpoint(
    IUserSearchRepository searchRepository,
    IUserRepository userRepository) : EndpointWithoutRequest<SearchUsersResponse>
{
    // Жёсткие лимиты для защиты от scraping / abuse. Клиентский page-size
    // выровнен под 20 (см. search-store.ts), серверный hard-cap — тот же.
    private const int DefaultLimit = 20;
    private const int MaxLimit = 20;
    private const int MaxOffset = 200;
    private const int MinQueryLength = 2;

    public override void Configure()
    {
        // Внутри сервиса путь — /search (FastEndpoints автоматически добавит api/v1 prefix).
        // Через гейтвей: /api/users/search → /api/v1/search (после PathRemovePrefix /api/users).
        Get("/search");
        Summary(s =>
        {
            s.Summary = "Search users by name / username / email for direct chat initiation";
            s.Description = "Returns up to 20 users matching the query, excluding the requester themself. "
                + "Combines prefix match, fuzzy (pg_trgm), full-text (tsvector), and bidirectional transliteration.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Биндим query-параметры вручную через FastEndpoints API.
        // QueryParam-атрибут на DTO работает капризно с web-кодировкой; явный Query<T> надёжнее.
        var rawQuery = Query<string?>("q", isRequired: false) ?? string.Empty;
        var offsetParam = Query<int?>("offset", isRequired: false);
        var limitParam = Query<int?>("limit", isRequired: false);

        var query = rawQuery.Trim();
        if (query.Length < MinQueryLength)
        {
            AddError("q", $"Query must be at least {MinQueryLength} characters.");
            ThrowIfAnyErrors(StatusCodes.Status400BadRequest);
        }

        var limit = Math.Clamp(limitParam ?? DefaultLimit, 1, MaxLimit);
        var offset = Math.Clamp(offsetParam ?? 0, 0, MaxOffset);

        var requesterId = HttpContext.User.GetUserId();

        // Запрашиваем limit+1, чтобы определить HasMore без отдельного COUNT-запроса.
        var hits = await searchRepository.SearchAsync(query, requesterId, offset, limit + 1, ct)
            .ConfigureAwait(false);

        var hasMore = hits.Count > limit;
        var page = hasMore ? hits.Take(limit).ToList() : hits.ToList();

        // Bulk-load аватаров одним SQL по UserRepository (он уже есть для BatchGetUsers).
        // Юзера без user_profile (ещё не открывал приложение) — аватара нет → null.
        var avatars = await userRepository.GetAvatarUrlsAsync(
            page.Select(h => h.UserId).ToArray(), ct).ConfigureAwait(false);

        var items = page.Select(h => new SearchUserDto(
            Id: h.UserId,
            DisplayName: h.DisplayName,
            Username: h.Username,
            AvatarUrl: avatars.TryGetValue(h.UserId, out var url) ? url : null))
            .ToList();

        await HttpContext.Response.SendAsync(
            new SearchUsersResponse(items, hasMore), cancellation: ct).ConfigureAwait(false);
    }
}
