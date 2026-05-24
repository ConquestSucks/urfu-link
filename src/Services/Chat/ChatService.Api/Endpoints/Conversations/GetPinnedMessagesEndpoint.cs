using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class GetPinnedMessagesEndpoint(GetPinnedMessagesQuery query)
    : EndpointWithoutRequest<IReadOnlyList<MessageDto>>
{
    public override void Configure()
    {
        Get("conversations/{id}/pinned");
        Group<ChatGroup>();
        Summary(s => s.Summary = "Return pinned messages for a conversation. Authz: caller must be a participant.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var id = Route<string>("id")!;
        var pinned = await query.ExecuteAsync(id, caller, ct).ConfigureAwait(false);
        await Send.OkAsync(pinned, ct).ConfigureAwait(false);
    }
}
