using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Urfu.Link.Services.Chat.Application.Presence;
using PresenceGrpc = Urfu.Link.Services.Presence.Grpc;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

/// <summary>
/// Production gRPC implementation of <see cref="IPresenceServiceClient"/>. Wraps the
/// <c>SetTyping</c> rpc on PresenceService so domain code never sees protobuf types.
/// </summary>
/// <remarks>
/// Conversation ids in chat are deterministic strings (SHA1 hex for direct,
/// <c>discipline:GUID</c> for groups). PresenceService stores typing per-conversation
/// by GUID, so we map those strings to a stable GUID that the client can derive too.
/// </remarks>
internal sealed class PresenceServiceClient(
    PresenceGrpc.InternalApi.InternalApiClient grpcClient,
    IGrpcBearerTokenProvider tokenProvider,
    ILogger<PresenceServiceClient> logger) : IPresenceServiceClient
{
    private const string DisciplinePrefix = "discipline:";

    public async Task SetTypingAsync(
        string conversationId,
        Guid userId,
        bool isTyping,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var conversationGuid = MapToConversationGuid(conversationId);
        var request = new PresenceGrpc.SetTypingRequest
        {
            ConversationId = conversationGuid.ToString("D", CultureInfo.InvariantCulture),
            UserId = userId.ToString("D", CultureInfo.InvariantCulture),
            IsTyping = isTyping,
        };

        try
        {
            var headers = await tokenProvider.GetAuthorizationMetadataAsync(cancellationToken).ConfigureAwait(false);
            await grpcClient
                .SetTypingAsync(request, headers, cancellationToken: cancellationToken)
                .ResponseAsync
                .ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            // Fail open: chat-side authorization already passed. Dropping the typing
            // signal is preferable to surfacing the failure as a SignalR exception.
            logger.LogWarning(
                ex,
                "PresenceService SetTyping failed for {ConversationId}/{UserId}; ignoring.",
                conversationId,
                userId);
        }
    }

    /// <summary>
    /// PresenceService keys typing by <see cref="Guid"/>. Direct conversation ids in chat are
    /// deterministic SHA1 strings; map them to a stable GUID. Discipline conversation ids
    /// already embed the GUID — extract it.
    /// </summary>
    internal static Guid MapToConversationGuid(string conversationId)
    {
        if (conversationId.StartsWith(DisciplinePrefix, StringComparison.Ordinal))
        {
            var guidPart = conversationId[DisciplinePrefix.Length..];
            if (Guid.TryParse(guidPart, out var disciplineId))
            {
                return disciplineId;
            }
        }

        if (Guid.TryParse(conversationId, out var guid))
        {
            return guid;
        }

        // Direct conversation ids are already SHA1 hex strings. Use the first 16 bytes
        // as a UUID-shaped key so PresenceHub events can be matched back to the chat id
        // by clients without async hashing in render-time selectors.
        if (conversationId.Length >= 32
            && conversationId[..32].All(IsHexDigit)
            && Guid.TryParseExact(conversationId[..32], "N", out var directId))
        {
            return directId;
        }

        // Fallback for any future non-hex ids: derive a stable Guid via SHA1 truncation.
#pragma warning disable CA5350
        Span<byte> hash = stackalloc byte[20];
        System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(conversationId), hash);
#pragma warning restore CA5350
        return new Guid(hash[..16]);
    }

    private static bool IsHexDigit(char value)
        => value is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';
}
