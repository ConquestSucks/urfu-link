using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class DeleteMessageRouteRequest
{
    public Guid Id { get; set; }

    [QueryParam] public string? Mode { get; set; }
}

public sealed class DeleteMessageEndpoint(DeleteMessageService service)
    : Endpoint<DeleteMessageRouteRequest, MessageDto?>
{
    public override void Configure()
    {
        Delete("messages/{id}");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Delete a message. Use mode=for-me for personal hide or mode=for-everyone (author-only).");
    }

    public override async Task HandleAsync(DeleteMessageRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var mode = ParseMode(req.Mode);
        var dto = await service.DeleteAsync(
            new DeleteMessageRequest(req.Id, caller, mode), ct).ConfigureAwait(false);
        if (dto is null)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }
        await Send.OkAsync(dto, ct).ConfigureAwait(false);
    }

    private static DeleteMode ParseMode(string? raw)
    {
        return raw switch
        {
            "for-everyone" => DeleteMode.ForEveryone,
            null or "" or "for-me" => DeleteMode.ForMe,
            _ => throw new ArgumentException(
                $"Unsupported delete mode '{raw}'. Use 'for-me' or 'for-everyone'.",
                nameof(raw)),
        };
    }
}
