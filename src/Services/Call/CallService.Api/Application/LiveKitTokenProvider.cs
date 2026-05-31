using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Urfu.Link.Services.Call.Application;

public sealed class LiveKitTokenProvider(IOptions<LiveKitOptions> options, TimeProvider timeProvider)
{
    public (string Token, DateTimeOffset ExpiresAtUtc) CreateJoinToken(
        string roomName,
        Guid participantId,
        string participantName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantName);

        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey) || string.IsNullOrWhiteSpace(opts.ApiSecret))
        {
            throw new InvalidOperationException("LiveKit API key/secret are not configured.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(opts.TokenTtl);
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = opts.ApiKey,
            ["sub"] = participantId.ToString("D"),
            ["name"] = participantName,
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["video"] = new Dictionary<string, object?>
            {
                ["room"] = roomName,
                ["roomJoin"] = true,
                ["canPublish"] = true,
                ["canSubscribe"] = true,
                ["canPublishData"] = true,
            },
        };

        var token = SignJwt(payload, opts.ApiSecret);
        return (token, expiresAt);
    }

    private static string SignJwt(IReadOnlyDictionary<string, object?> payload, string secret)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };

        var headerPart = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadPart = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerPart}.{payloadPart}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput)));
        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
