using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class UnpinMessageRouteRequest
{
    public string Id { get; set; } = string.Empty;

    public Guid MessageId { get; set; }
}

public sealed class UnpinMessageEndpoint(UnpinMessageService service)
    : Endpoint<UnpinMessageRouteRequest, IReadOnlyList<MessageDto>>
{
    public override void Configure()
    {
        Delete("conversations/{id}/pinned/{messageId}");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Unpin a message. Same authz as pinning.");
    }

    public override async Task HandleAsync(UnpinMessageRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var dtos = await service.UnpinAsync(
            new UnpinMessageRequest(req.Id, caller, req.MessageId), ct).ConfigureAwait(false);
        await Send.OkAsync(dtos, ct).ConfigureAwait(false);
    }
}
