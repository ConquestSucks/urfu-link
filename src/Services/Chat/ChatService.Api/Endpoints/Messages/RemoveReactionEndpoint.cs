using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class RemoveReactionRouteRequest
{
    public Guid Id { get; set; }

    public string Emoji { get; set; } = string.Empty;
}

public sealed class RemoveReactionEndpoint(RemoveReactionService service)
    : Endpoint<RemoveReactionRouteRequest>
{
    public override void Configure()
    {
        Delete("messages/{id}/reactions/{emoji}");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Remove the caller's reaction emoji from a message.");
    }

    public override async Task HandleAsync(RemoveReactionRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        await service.RemoveAsync(
            new RemoveReactionRequest(req.Id, caller, req.Emoji ?? string.Empty), ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
