using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record GetNotificationRequest(Guid Id);

public sealed class GetNotificationEndpoint(INotificationRepository repository)
    : Endpoint<GetNotificationRequest, NotificationDto>
{
    public override void Configure()
    {
        Get("/me/notifications/{Id:guid}");
        Summary(s => s.Summary = "Get a single notification by id");
    }

    public override async Task HandleAsync(GetNotificationRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();

        var notification = await repository.GetByIdAsync(req.Id, userId, ct).ConfigureAwait(false);
        if (notification is null)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        await Send.OkAsync(NotificationDtoMapper.Map(notification), ct).ConfigureAwait(false);
    }
}
