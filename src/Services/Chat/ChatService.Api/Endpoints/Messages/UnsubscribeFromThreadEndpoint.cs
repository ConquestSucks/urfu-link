using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class UnsubscribeFromThreadRouteRequest
{
    public Guid Id { get; set; }
}

public sealed class UnsubscribeFromThreadEndpoint(LeaveThreadService service)
    : Endpoint<UnsubscribeFromThreadRouteRequest>
{
    public override void Configure()
    {
        Delete("messages/{id}/thread/subscribe");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Remove the caller's subscription from a thread. Reply history is untouched.");
    }

    public override async Task HandleAsync(UnsubscribeFromThreadRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        await service.LeaveAsync(new LeaveThreadRequest(caller, req.Id), ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
