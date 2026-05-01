using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Infrastructure.Auth;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed class GetBadgeEndpoint(BadgeService service) : EndpointWithoutRequest<BadgeSnapshotDto>
{
    public override void Configure()
    {
        Get("/me/notifications/badge");
        Summary(s => s.Summary = "Get total and per-category unread badge counters");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var snapshot = await service.GetSnapshotAsync(userId, ct).ConfigureAwait(false);
        await Send.OkAsync(snapshot, ct).ConfigureAwait(false);
    }
}
