using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record MarkReadRequest(Guid Id);

public sealed class MarkReadEndpoint(MarkAsReadService service)
    : Endpoint<MarkReadRequest>
{
    public override void Configure()
    {
        Post("/me/notifications/{Id:guid}/read");
        Summary(s => s.Summary = "Mark a single notification as read");
    }

    public override async Task HandleAsync(MarkReadRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        await service.MarkSingleAsync(userId, req.Id, ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
