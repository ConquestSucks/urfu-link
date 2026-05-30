using Urfu.Link.Services.Call.Application.Chat;
using Urfu.Link.Services.Chat.Grpc;

namespace Urfu.Link.Services.Call.Infrastructure.Grpc;

public sealed class ChatConversationClient(InternalApi.InternalApiClient client) : IChatConversationClient
{
    public async Task<CallConversationMetadata> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        var reply = await client.GetConversationAsync(
            new GetConversationRequest { ConversationId = conversationId },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new CallConversationMetadata(
            reply.Exists,
            reply.Type == ConversationKind.Group ? "Group" : "Direct",
            reply.Participants
                .Select(Guid.Parse)
                .ToList());
    }
}
