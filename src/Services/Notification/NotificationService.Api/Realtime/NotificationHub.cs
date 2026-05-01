using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Realtime;

[Authorize]
public sealed class NotificationHub(IBadgeStore badgeStore, MarkAsReadService markAsRead)
    : Hub<INotificationClient>
{
    private readonly IBadgeStore _badgeStore = badgeStore;
    private readonly MarkAsReadService _markAsRead = markAsRead;

    public static string GroupForUser(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId), Context.ConnectionAborted)
            .ConfigureAwait(false);

        var snapshot = await _badgeStore.GetSnapshotAsync(userId, Context.ConnectionAborted).ConfigureAwait(false);
        await Clients.Caller.BadgeUpdated(MapSnapshot(snapshot)).ConfigureAwait(false);

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
        var snapshot = await _badgeStore.GetSnapshotAsync(userId, Context.ConnectionAborted).ConfigureAwait(false);
        return MapSnapshot(snapshot);
    }

    internal static BadgeSnapshotDto MapSnapshot(BadgeSnapshot snapshot)
    {
        var perCategory = snapshot.PerCategory.ToDictionary(kv => (int)kv.Key, kv => kv.Value);
        return new BadgeSnapshotDto(snapshot.Total, perCategory);
    }
}
