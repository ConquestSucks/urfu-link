using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Endpoints;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public sealed class EnterpriseNotificationCenterTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Badge_IncludesUnreadUnseenUrgentAndTypeCounters()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);
        var urgent = await SeedAsync(userId, NotificationCategory.CallIncoming, NotificationSeverity.Urgent);
        _ = urgent;
        var seen = await SeedAsync(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Normal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var storedSeen = await db.Notifications.SingleAsync(n => n.Id == seen);
            storedSeen.MarkSeen(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();

            var badgeStore = scope.ServiceProvider.GetRequiredService<IBadgeStore>();
            await badgeStore.IncrementAsync(userId, NotificationCategory.CallIncoming, default);
            await badgeStore.IncrementAsync(userId, NotificationCategory.ChatMessageDirect, default);
        }

        using var client = factory.CreateClient();
        var badge = await client.GetFromJsonAsync<BadgeSnapshotDto>("/api/v1/me/notifications/badge");

        badge!.Total.Should().Be(2);
        badge.TotalUnseen.Should().Be(1);
        badge.UrgentUnread.Should().Be(1);
        badge.PerType.Should().ContainKey("call.incoming");
    }

    [Fact]
    public async Task ListNotifications_CanFilterByStatusTypeSeverityAndQuery()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);
        var savedId = await SeedAsync(
            userId,
            NotificationCategory.ChatMessageMention,
            NotificationSeverity.High,
            "Mention",
            "Teacher mentioned you");
        await SeedAsync(
            userId,
            NotificationCategory.DisciplineDeadline,
            NotificationSeverity.Normal,
            "Deadline",
            "Lab closes soon");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var saved = await db.Notifications.SingleAsync(n => n.Id == savedId);
            saved.Save(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<ListNotificationsResponse>(
            "/api/v1/me/notifications?status=saved&type=chat.mention&severity=2&query=teacher");

        response!.Items.Should().ContainSingle();
        response.Items[0].Id.Should().Be(savedId);
        response.Items[0].SavedAtUtc.Should().NotBeNull();
        response.Items[0].Type.Should().Be("chat.mention");
    }

    [Fact]
    public async Task IndividualStateActions_UpdateNotificationAndBadge()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);
        var notificationId = await SeedAsync(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Normal);
        await IncrementBadgeAsync(userId, NotificationCategory.ChatMessageDirect);

        using var client = factory.CreateClient();

        var seen = await client.PostAsync($"/api/v1/me/notifications/{notificationId}/seen", null);
        var saved = await client.PostAsync($"/api/v1/me/notifications/{notificationId}/save", null);
        var done = await client.PostAsync($"/api/v1/me/notifications/{notificationId}/done", null);

        seen.StatusCode.Should().Be(HttpStatusCode.NoContent);
        saved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        done.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var stored = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == notificationId);
        stored.SeenAtUtc.Should().NotBeNull();
        stored.SavedAtUtc.Should().NotBeNull();
        stored.DoneAtUtc.Should().NotBeNull();
        stored.ReadAtUtc.Should().NotBeNull();

        var badge = await scope.ServiceProvider.GetRequiredService<IBadgeStore>().GetSnapshotAsync(userId, default);
        badge.Total.Should().Be(0);
    }

    [Fact]
    public async Task BulkAction_CanMarkMatchingUnreadNotificationsDone()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);
        await SeedAsync(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Normal);
        await SeedAsync(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Normal);
        await SeedAsync(userId, NotificationCategory.DisciplineDeadline, NotificationSeverity.Normal);
        await IncrementBadgeAsync(userId, NotificationCategory.ChatMessageDirect);
        await IncrementBadgeAsync(userId, NotificationCategory.ChatMessageDirect);
        await IncrementBadgeAsync(userId, NotificationCategory.DisciplineDeadline);

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/me/notifications/bulk",
            new
            {
                Action = "done",
                Filter = new
                {
                    Category = (int)NotificationCategory.ChatMessageDirect,
                    Status = "unread",
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BulkNotificationActionResponse>();
        result!.Updated.Should().Be(2);

        var badge = await client.GetFromJsonAsync<BadgeSnapshotDto>("/api/v1/me/notifications/badge");
        badge!.Total.Should().Be(1);
        badge.PerCategory.Should().ContainKey((int)NotificationCategory.DisciplineDeadline);
    }

    private async Task<Guid> SeedAsync(
        Guid userId,
        NotificationCategory category,
        NotificationSeverity severity,
        string title = "Title",
        string body = "Body")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notification = NotificationAggregate.Create(
            recipientUserId: userId,
            category: category,
            severity: severity,
            content: NotificationContent.Create(title, body),
            data: NotificationData.Empty,
            groupKey: null,
            sourceEventId: Guid.NewGuid(),
            sourceEventType: "test.v1",
            createdAtUtc: DateTimeOffset.UtcNow);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }

    private async Task IncrementBadgeAsync(Guid userId, NotificationCategory category)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var badgeStore = scope.ServiceProvider.GetRequiredService<IBadgeStore>();
        await badgeStore.IncrementAsync(userId, category, CancellationToken.None);
    }
}
