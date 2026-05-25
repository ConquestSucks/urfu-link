using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed class NotificationStateEndpoint(MarkAsReadService service)
    : EndpointWithoutRequest
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "read",
        "unread",
        "seen",
        "save",
        "unsave",
        "done",
        "restore",
        "archive",
    };

    public override void Configure()
    {
        Post("/me/notifications/{Id:guid}/{Action}");
        Summary(s => s.Summary = "Apply a state transition to a notification");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("Id");
        var action = Route<string>("Action") ?? string.Empty;
        if (!AllowedActions.Contains(action))
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        var userId = HttpContext.User.GetUserId();
        await service.ApplyActionAsync(userId, id, action, ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
