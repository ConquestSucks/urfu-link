using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Keycloak;

// Получает «контактную» часть профиля (имя, email) из Keycloak Admin API.
// Использует тот же client_credentials grant, что и KeycloakSessionClient,
// но это отдельный класс — чтобы Session API (DELETE /sessions/*) не смешивался
// с пользовательским lookup'ом.
public sealed class KeycloakUserClient(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakUserClient> logger) : IUserDirectory, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly KeycloakAdminOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry;

    public void Dispose() => _tokenLock.Dispose();

    public async Task<IReadOnlyDictionary<Guid, UserDirectoryEntry>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
            return new Dictionary<Guid, UserDirectoryEntry>();

        var distinct = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinct.Length == 0)
            return new Dictionary<Guid, UserDirectoryEntry>();

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        // Ограничиваем concurrency, чтобы при ChatService.GetParticipants на чат из 50 человек
        // не положить Keycloak. 8 параллельных запросов — компромисс между latency и нагрузкой.
        var gate = new SemaphoreSlim(8, 8);
        try
        {
            var tasks = distinct.Select(id => FetchOneAsync(id, token, gate, cancellationToken));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return results
                .Where(r => r is not null)
                .ToDictionary(r => r!.UserId, r => r!);
        }
        finally
        {
            gate.Dispose();
        }
    }

    private async Task<UserDirectoryEntry?> FetchOneAsync(
        Guid userId,
        string accessToken,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var url = $"{_options.AdminUrl}/admin/realms/{_options.Realm}/users/{userId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Keycloak user lookup failed for {UserId}: {Status}",
                    userId, response.StatusCode);
                return null;
            }

            var dto = await response.Content
                .ReadFromJsonAsync<KeycloakUser>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (dto is null)
                return null;

            return new UserDirectoryEntry(
                UserId: userId,
                DisplayName: BuildDisplayName(dto),
                Email: dto.Email ?? string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<UserDirectoryPageItem>> ListPageAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        // briefRepresentation=true оставляет только базовые поля; sort по id
        // в KC не поддерживается через REST, но порядок страниц стабилен —
        // достаточно для пейджинга в reconciler-е.
        var url = $"{_options.AdminUrl}/admin/realms/{_options.Realm}/users"
            + $"?first={offset}&max={limit}&briefRepresentation=true";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Keycloak users list failed at offset={Offset} limit={Limit}: {Status}",
                offset, limit, response.StatusCode);
            return Array.Empty<UserDirectoryPageItem>();
        }

        var page = await response.Content
            .ReadFromJsonAsync<KeycloakUser[]>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (page is null || page.Length == 0)
            return Array.Empty<UserDirectoryPageItem>();

        var items = new List<UserDirectoryPageItem>(page.Length);
        foreach (var dto in page)
        {
            if (!Guid.TryParse(dto.Id, out var id) || id == Guid.Empty)
                continue;

            // KC отдаёт createdTimestamp всегда, modifiedTimestamp — только при
            // включённом фиче-флаге в реалме. Используем max(modified, created),
            // чтобы idempotency-маркер всегда был ненулевым.
            var modifiedMs = Math.Max(dto.ModifiedTimestamp ?? 0, dto.CreatedTimestamp ?? 0);

            items.Add(new UserDirectoryPageItem(
                UserId: id,
                Username: dto.Username ?? string.Empty,
                FirstName: dto.FirstName,
                LastName: dto.LastName,
                Email: dto.Email,
                DisplayName: BuildDisplayName(dto),
                ModifiedTimestampMs: modifiedMs));
        }

        return items;
    }

    private static string BuildDisplayName(KeycloakUser dto)
    {
        var first = (dto.FirstName ?? string.Empty).Trim();
        var last = (dto.LastName ?? string.Empty).Trim();
        var full = string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(full))
            return full;
        if (!string.IsNullOrWhiteSpace(dto.Username))
            return dto.Username;
        return dto.Email ?? string.Empty;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _accessToken;

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _accessToken;

            var tokenUrl = $"{_options.AdminUrl}/realms/{_options.Realm}/protocol/openid-connect/token";
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
            });

            var response = await httpClient.PostAsync(new Uri(tokenUrl), content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content
                .ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            _accessToken = tokenResponse!.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed class KeycloakUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("createdTimestamp")]
        public long? CreatedTimestamp { get; set; }

        [JsonPropertyName("modifiedTimestamp")]
        public long? ModifiedTimestamp { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
