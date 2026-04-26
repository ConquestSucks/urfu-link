using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class SubscribeToThreadRouteRequest
{
    public Guid Id { get; set; }
}

public sealed class SubscribeToThreadEndpoint(JoinThreadService service)
    : Endpoint<SubscribeToThreadRouteRequest>
{
    public override void Configure()
    {
        Post("messages/{id}/thread/subscribe");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Manually subscribe to thread updates rooted at the given message.");
    }

    public override async Task HandleAsync(SubscribeToThreadRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        await service.JoinAsync(new JoinThreadRequest(caller, req.Id), ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
