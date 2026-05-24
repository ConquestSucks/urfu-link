using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

internal sealed class KeycloakClientCredentialsGrpcBearerTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<InternalGrpcAuthOptions> options,
    ILogger<KeycloakClientCredentialsGrpcBearerTokenProvider> logger,
    TimeProvider clock) : IGrpcBearerTokenProvider, IDisposable
{
    internal const string HttpClientName = "chat-internal-grpc-auth";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry;

    public void Dispose()
    {
        _httpClient.Dispose();
        _tokenLock.Dispose();
    }

    public async ValueTask<Metadata?> GetAuthorizationMetadataAsync(CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return new Metadata
        {
            { "authorization", $"Bearer {token}" },
        };
    }

    private async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var auth = options.Value;
        if (!auth.IsConfigured())
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (_accessToken is not null && now < _tokenExpiry)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = clock.GetUtcNow();
            if (_accessToken is not null && now < _tokenExpiry)
            {
                return _accessToken;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = auth.ClientId,
                ["client_secret"] = auth.ClientSecret,
            });

            using var response = await _httpClient
                .PostAsync(new Uri(auth.TokenEndpoint), content, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Internal gRPC token request failed with HTTP {StatusCode}.",
                    response.StatusCode);
                return null;
            }

            var tokenResponse = await response.Content
                .ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                logger.LogWarning("Internal gRPC token response did not contain an access token.");
                return null;
            }

            var refreshSkew = auth.RefreshSkew < TimeSpan.Zero ? TimeSpan.Zero : auth.RefreshSkew;
            var expiresIn = Math.Max(tokenResponse.ExpiresIn, 0);
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = now.AddSeconds(expiresIn) - refreshSkew;

            return _accessToken;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Internal gRPC token request failed.");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Internal gRPC token response could not be parsed.");
            return null;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Internal gRPC token request timed out.");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
