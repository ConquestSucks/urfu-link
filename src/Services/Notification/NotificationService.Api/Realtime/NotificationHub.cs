using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Realtime;

[Authorize]
public sealed class NotificationHub(BadgeService badgeService, MarkAsReadService markAsRead)
    : Hub<INotificationClient>
{
    private readonly BadgeService _badgeService = badgeService;
    private readonly MarkAsReadService _markAsRead = markAsRead;

    public static string GroupForUser(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId), Context.ConnectionAborted)
            .ConfigureAwait(false);

        var snapshot = await _badgeService.GetSnapshotAsync(userId, Context.ConnectionAborted).ConfigureAwait(false);
        await Clients.Caller.BadgeUpdated(snapshot).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public Task MarkAsRead(Guid notificationId)
    {
        var userId = Context.User!.GetUserId();
        return _markAsRead.MarkSingleAsync(userId, notificationId, Context.ConnectionAborted);
    }

    public Task MarkAllAsRead(int? category)
    {
        var userId = Context.User!.GetUserId();
        return _markAsRead.MarkAllAsync(userId, category.HasValue ? (NotificationCategory)category.Value : null, Context.ConnectionAborted);
    }

    public async Task<BadgeSnapshotDto> GetBadge()
    {
        var userId = Context.User!.GetUserId();
        return await _badgeService.GetSnapshotAsync(userId, Context.ConnectionAborted).ConfigureAwait(false);
    }
}
