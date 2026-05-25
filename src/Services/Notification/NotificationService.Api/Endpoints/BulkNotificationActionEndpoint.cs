using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record BulkNotificationActionRequest(
    string Action,
    IReadOnlyList<Guid>? Ids,
    BulkNotificationFilter? Filter);

public sealed record BulkNotificationFilter(
    int? Category,
    string? Type,
    int? Severity,
    string? Status,
    string? Query,
    DateTimeOffset? From,
    DateTimeOffset? To);

public sealed record BulkNotificationActionResponse(int Updated);

public sealed class BulkNotificationActionEndpoint(MarkAsReadService service)
    : Endpoint<BulkNotificationActionRequest, BulkNotificationActionResponse>
{
    public override void Configure()
    {
        Post("/me/notifications/bulk");
        Summary(s => s.Summary = "Apply a notification state action to selected ids or a filter");
    }

    public override async Task HandleAsync(BulkNotificationActionRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var filter = Map(req.Filter);

        var updated = await service.ApplyBulkAsync(
            userId,
            req.Ids,
            filter,
            req.Action,
            ct).ConfigureAwait(false);

        await Send.OkAsync(new BulkNotificationActionResponse(updated), ct).ConfigureAwait(false);
    }

    private static NotificationListFilter Map(BulkNotificationFilter? filter)
    {
        if (filter is null)
        {
            return new NotificationListFilter();
        }

        return new NotificationListFilter(
            filter.Category.HasValue ? (NotificationCategory)filter.Category.Value : null,
            filter.Type,
            filter.Severity.HasValue ? (NotificationSeverity)filter.Severity.Value : null,
            filter.Status,
            filter.Query,
            filter.From,
            filter.To);
    }
}
