using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class GetReadReceiptsRouteRequest
{
    public Guid Id { get; set; }
}

public sealed class GetReadReceiptsEndpoint(GetReadReceiptsQuery query)
    : Endpoint<GetReadReceiptsRouteRequest, IReadOnlyList<ReadReceiptDto>>
{
    public override void Configure()
    {
        Get("messages/{id}/read-receipts");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Read receipts for a message. Authz: caller must be a participant of the conversation.");
    }

    public override async Task HandleAsync(GetReadReceiptsRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var receipts = await query.ExecuteAsync(req.Id, caller, ct).ConfigureAwait(false);
        await Send.OkAsync(receipts, ct).ConfigureAwait(false);
    }
}
