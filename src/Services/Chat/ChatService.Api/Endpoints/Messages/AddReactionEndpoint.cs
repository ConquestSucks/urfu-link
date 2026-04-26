using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class AddReactionRouteRequest
{
    public Guid Id { get; set; }

    public string Emoji { get; set; } = string.Empty;
}

public sealed class AddReactionEndpoint(AddReactionService service)
    : Endpoint<AddReactionRouteRequest>
{
    public override void Configure()
    {
        Post("messages/{id}/reactions");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Add or replace the caller's reaction emoji on a message.");
    }

    public override async Task HandleAsync(AddReactionRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        await service.AddAsync(
            new AddReactionRequest(req.Id, caller, req.Emoji ?? string.Empty), ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
