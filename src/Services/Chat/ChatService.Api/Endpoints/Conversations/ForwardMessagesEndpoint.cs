using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class ForwardMessagesRouteRequest
{
    public string Id { get; set; } = string.Empty;

    public IReadOnlyList<Guid> MessageIds { get; set; } = Array.Empty<Guid>();
}

public sealed class ForwardMessagesEndpoint(ForwardMessagesService service)
    : Endpoint<ForwardMessagesRouteRequest, IReadOnlyList<MessageDto>>
{
    public override void Configure()
    {
        Post("conversations/{id}/forward");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Forward up to MaxForwardedMessages messages into a target conversation.");
    }

    public override async Task HandleAsync(ForwardMessagesRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var dtos = await service.ForwardAsync(
            new ForwardMessagesRequest(req.Id, caller, req.MessageIds ?? Array.Empty<Guid>()), ct)
            .ConfigureAwait(false);
        await Send.OkAsync(dtos, ct).ConfigureAwait(false);
    }
}
