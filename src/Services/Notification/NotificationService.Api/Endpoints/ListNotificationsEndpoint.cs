using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record ListNotificationsRequest(
    string? Cursor,
    int? Limit,
    int? Category,
    bool? UnreadOnly);

public sealed record ListNotificationsResponse(
    IReadOnlyList<NotificationDto> Items,
    string? NextCursor);

public sealed class ListNotificationsEndpoint(INotificationRepository repository)
    : Endpoint<ListNotificationsRequest, ListNotificationsResponse>
{
    public override void Configure()
    {
        Get("/me/notifications");
        Summary(s => s.Summary = "List the caller's notifications (cursor-paginated, newest first)");
    }

    public override async Task HandleAsync(ListNotificationsRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var limit = Math.Clamp(req.Limit ?? 20, 1, 100);
        Cursor.TryDecode(req.Cursor, out var cursorTs, out var cursorId);

        NotificationCategory? category = req.Category.HasValue
            ? (NotificationCategory)req.Category.Value
            : null;

        var notifications = await repository.ListAsync(
            userId,
            category,
            req.UnreadOnly ?? false,
            cursorTs == default ? null : cursorTs,
            cursorId == Guid.Empty ? null : cursorId,
            limit,
            ct).ConfigureAwait(false);

        var items = notifications.Select(NotificationDtoMapper.Map).ToList();
        var nextCursor = items.Count == limit
            ? Cursor.Encode(notifications[^1].CreatedAtUtc, notifications[^1].Id)
            : null;

        await Send.OkAsync(new ListNotificationsResponse(items, nextCursor), ct).ConfigureAwait(false);
    }
}
