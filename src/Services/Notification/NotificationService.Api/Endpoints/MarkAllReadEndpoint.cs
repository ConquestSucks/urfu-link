using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record MarkAllReadRequest(int? Category);

public sealed record MarkAllReadResponse(int Updated);

public sealed class MarkAllReadEndpoint(MarkAsReadService service)
    : Endpoint<MarkAllReadRequest, MarkAllReadResponse>
{
    public override void Configure()
    {
        Post("/me/notifications/read-all");
        Summary(s => s.Summary = "Mark every unread notification as read (optionally per-category)");
    }

    public override async Task HandleAsync(MarkAllReadRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var category = req.Category.HasValue ? (NotificationCategory)req.Category.Value : (NotificationCategory?)null;

        var updated = await service.MarkAllAsync(userId, category, ct).ConfigureAwait(false);
        await Send.OkAsync(new MarkAllReadResponse(updated), ct).ConfigureAwait(false);
    }
}
