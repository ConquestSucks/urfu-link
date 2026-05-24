using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class ReplyInThreadRouteRequest
{
    public Guid Id { get; set; }

    public string Body { get; set; } = string.Empty;

    public IReadOnlyList<Guid> AttachmentAssetIds { get; set; } = Array.Empty<Guid>();

    public Guid? ReplyToMessageId { get; set; }

    public string ClientMessageId { get; set; } = string.Empty;
}

public sealed class ReplyInThreadEndpoint(ReplyInThreadService service)
    : Endpoint<ReplyInThreadRouteRequest, MessageDto>
{
    public override void Configure()
    {
        Post("messages/{id}/thread");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Post a reply in the thread rooted at the given message.");
    }

    public override async Task HandleAsync(ReplyInThreadRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var dto = await service.ReplyAsync(
            new ReplyInThreadRequest(
                caller,
                req.Id,
                req.Body ?? string.Empty,
                req.AttachmentAssetIds ?? Array.Empty<Guid>(),
                req.ReplyToMessageId,
                req.ClientMessageId ?? string.Empty),
            ct).ConfigureAwait(false);
        await Send.OkAsync(dto, ct).ConfigureAwait(false);
    }
}
