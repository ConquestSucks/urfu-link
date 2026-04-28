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
/// Conversation ids in chat are deterministic strings (SHA1 for direct, <c>discipline:GUID</c>
/// for groups). PresenceService stores typing per-conversation by GUID, so for direct chats
/// we hash the deterministic string into a stable GUID before sending. Discipline
/// conversation ids embed the GUID directly — extract it.
/// </remarks>
internal sealed class PresenceServiceClient(
    PresenceGrpc.InternalApi.InternalApiClient grpcClient,
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
            await grpcClient
                .SetTypingAsync(request, cancellationToken: cancellationToken)
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

        // Direct conversation id — derive a stable Guid via SHA1 truncation. SHA1 is used
        // here as a non-cryptographic deterministic hash (same justification as
        // Conversation.OpenDirect): the same string maps to the same Guid every time so
        // PresenceService keys remain stable. Collisions would only matter for identifier
        // reuse, not security.
#pragma warning disable CA5350
        Span<byte> hash = stackalloc byte[20];
        System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(conversationId), hash);
#pragma warning restore CA5350
        return new Guid(hash[..16]);
    }
}
