using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class PinMessageRouteRequest
{
    public string Id { get; set; } = string.Empty;

    public Guid MessageId { get; set; }
}

public sealed class PinMessageEndpoint(PinMessageService service)
    : Endpoint<PinMessageRouteRequest, IReadOnlyList<MessageDto>>
{
    public override void Configure()
    {
        Post("conversations/{id}/pinned");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Pin a message in a conversation. Authz: any participant for direct, teacher-only for discipline (#214).");
    }

    public override async Task HandleAsync(PinMessageRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var dtos = await service.PinAsync(
            new PinMessageRequest(req.Id, caller, req.MessageId), ct).ConfigureAwait(false);
        await Send.OkAsync(dtos, ct).ConfigureAwait(false);
    }
}
