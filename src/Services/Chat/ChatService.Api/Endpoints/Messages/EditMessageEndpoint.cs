using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class EditMessageRouteRequest
{
    public Guid Id { get; set; }

    public string Body { get; set; } = string.Empty;
}

public sealed class EditMessageEndpoint(EditMessageService service)
    : Endpoint<EditMessageRouteRequest, MessageDto>
{
    public override void Configure()
    {
        Patch("messages/{id}");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Edit a message body. Author-only, allowed within the configured TTL.");
    }

    public override async Task HandleAsync(EditMessageRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var dto = await service.EditAsync(
            new EditMessageRequest(req.Id, caller, req.Body ?? string.Empty), ct).ConfigureAwait(false);
        await Send.OkAsync(dto, ct).ConfigureAwait(false);
    }
}
