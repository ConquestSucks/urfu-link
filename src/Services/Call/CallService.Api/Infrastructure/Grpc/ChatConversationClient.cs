using Urfu.Link.Services.Call.Application.Chat;
using Urfu.Link.Services.Chat.Grpc;

namespace Urfu.Link.Services.Call.Infrastructure.Grpc;

internal sealed class ChatConversationClient(
    InternalApi.InternalApiClient client,
    IGrpcBearerTokenProvider tokenProvider) : IChatConversationClient
{
    public async Task<CallConversationMetadata> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        var metadata = await tokenProvider.GetAuthorizationMetadataAsync(cancellationToken).ConfigureAwait(false);
        var reply = await client.GetConversationAsync(
            new GetConversationRequest { ConversationId = conversationId },
            headers: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new CallConversationMetadata(
            reply.Exists,
            reply.Type == ConversationKind.Group ? "Group" : "Direct",
            reply.Participants
                .Select(Guid.Parse)
                .ToList());
    }
}
