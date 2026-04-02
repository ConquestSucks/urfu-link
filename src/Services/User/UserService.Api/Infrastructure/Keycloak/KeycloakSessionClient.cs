using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Keycloak;

public sealed class KeycloakSessionClient(
    HttpClient httpClient,
    IOptions<KeycloakAdminOptions> options) : ISessionManager, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly KeycloakAdminOptions _options = options.Value;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry;

    public async Task<IReadOnlyList<DeviceSession>> GetSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        var url = $"{_options.AdminUrl}/admin/realms/{_options.Realm}/users/{userId}/sessions";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var sessions = await response.Content
            .ReadFromJsonAsync<List<KeycloakSession>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return sessions?.Select(s => new DeviceSession(
            SessionId: s.Id,
            IpAddress: s.IpAddress,
            LastAccess: DateTimeOffset.FromUnixTimeSeconds(s.LastAccess),
            Browser: ParseBrowser(s.Clients),
            Os: null)).ToList()
            ?? [];
    }

    public async Task TerminateAsync(string sessionId, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        var url = $"{_options.AdminUrl}/admin/realms/{_options.Realm}/sessions/{sessionId}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(url));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task TerminateAllExceptAsync(
        Guid userId,
        string currentSessionId,
        CancellationToken cancellationToken)
    {
        var sessions = await GetSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        var tasks = sessions
            .Where(s => !string.Equals(s.SessionId, currentSessionId, StringComparison.Ordinal))
            .Select(s => TerminateAsync(s.SessionId, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public void Dispose() => _tokenLock.Dispose();

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

    private static string? ParseBrowser(List<string>? clients)
    {
        return clients is { Count: > 0 } ? string.Join(", ", clients) : null;
    }

    private sealed class KeycloakSession
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        [JsonPropertyName("lastAccess")]
        public long LastAccess { get; set; }

        [JsonPropertyName("clients")]
        public List<string>? Clients { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
