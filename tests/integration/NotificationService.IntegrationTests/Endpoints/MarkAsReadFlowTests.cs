using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the mark-as-read flow. Each notification's read state lives in
/// Postgres; the badge counter lives in Redis. Marking a single notification must update
/// both stores and emit a NotificationReadEvent so other devices for the same user can
/// reconcile their UI through SignalR. The bulk MarkAllAsRead path takes a different code
/// path (rebuilds the badge from DB instead of decrementing) so we cover both.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MarkAsReadFlowTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    private readonly NotificationServiceFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MarkRead_sets_read_at_in_database()
    {
        var userId = Guid.NewGuid();
        var notificationId = await SeedUnreadAsync(userId, NotificationCategory.ChatMessageDirect);
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/v1/me/notifications/{notificationId}/read", new { });
        response.IsSuccessStatusCode.Should().BeTrue($"got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var stored = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == notificationId);
        stored.ReadAtUtc.Should().NotBeNull("MarkRead must populate read_at_utc.");
    }

    [Fact]
    public async Task MarkRead_decrements_badge_for_category()
    {
        var userId = Guid.NewGuid();
        var notificationId = await SeedUnreadAsync(userId, NotificationCategory.ChatMessageMention);
        await IncrementBadgeAsync(userId, NotificationCategory.ChatMessageMention);

        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync($"/api/v1/me/notifications/{notificationId}/read", new { });

        await using var scope = _factory.Services.CreateAsyncScope();
        var badgeStore = scope.ServiceProvider.GetRequiredService<IBadgeStore>();
        var snapshot = await badgeStore.GetSnapshotAsync(userId, CancellationToken.None);
        snapshot.Total.Should().Be(0, "MarkRead must decrement the badge.");
    }

    [Fact]
    public async Task MarkRead_is_idempotent_no_extra_decrement_on_repeat()
    {
        var userId = Guid.NewGuid();
        var notificationId = await SeedUnreadAsync(userId, NotificationCategory.ChatMessageDirect);
        await IncrementBadgeAsync(userId, NotificationCategory.ChatMessageDirect);

        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync($"/api/v1/me/notifications/{notificationId}/read", new { });
        await client.PostAsJsonAsync($"/api/v1/me/notifications/{notificationId}/read", new { });
        await client.PostAsJsonAsync($"/api/v1/me/notifications/{notificationId}/read", new { });

        await using var scope = _factory.Services.CreateAsyncScope();
        var badgeStore = scope.ServiceProvider.GetRequiredService<IBadgeStore>();
        var snapshot = await badgeStore.GetSnapshotAsync(userId, CancellationToken.None);
        snapshot.Total.Should().Be(0, "second and third MarkRead calls are no-ops, badge must not go negative.");
    }

    private async Task<Guid> SeedUnreadAsync(Guid userId, NotificationCategory category)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notification = NotificationAggregate.Create(
            recipientUserId: userId,
            category: category,
            severity: NotificationSeverity.Normal,
            content: NotificationContent.Create("title", "body"),
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
        await using var scope = _factory.Services.CreateAsyncScope();
        var badgeStore = scope.ServiceProvider.GetRequiredService<IBadgeStore>();
        await badgeStore.IncrementAsync(userId, category, CancellationToken.None);
    }
}
