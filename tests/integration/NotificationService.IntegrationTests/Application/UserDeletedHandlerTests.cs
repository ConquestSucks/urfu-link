using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.IntegrationTests.Application;

/// <summary>
/// User deletion is irreversible upstream — NotificationService must clean up the user's
/// push devices (so we never blast a deleted user's old phone with a stranger's
/// notifications) and drop unread notifications. Read history is preserved for audit.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class UserDeletedHandlerTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    private readonly NotificationServiceFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Deactivates_active_push_devices_for_user()
    {
        var userId = Guid.NewGuid();
        await SeedActivePushDevice(userId);

        await HandleAsync(new UserDeletedEvent(userId));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var devices = await db.PushDevices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync();
        devices.Should().NotBeEmpty();
        devices.Should().AllSatisfy(d => d.IsActive.Should().BeFalse());
    }

    [Fact]
    public async Task Drops_unread_notifications_only()
    {
        var userId = Guid.NewGuid();
        var unreadId = await SeedNotification(userId, readAt: null);
        var readId = await SeedNotification(userId, readAt: DateTimeOffset.UtcNow.AddHours(-1));

        await HandleAsync(new UserDeletedEvent(userId));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var remaining = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .Select(n => n.Id)
            .ToListAsync();

        remaining.Should().NotContain(unreadId, "unread notifications must be dropped on user delete.");
        remaining.Should().Contain(readId, "read notifications stay for audit.");
    }

    [Fact]
    public async Task Is_idempotent_when_user_has_no_devices_or_notifications()
    {
        var userId = Guid.NewGuid();

        // Should not throw — handler may be retried by Kafka after a transient failure.
        await HandleAsync(new UserDeletedEvent(userId));
        await HandleAsync(new UserDeletedEvent(userId));
    }

    private async Task HandleAsync(UserDeletedEvent integrationEvent)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<UserDeletedHandler>();
        await handler.HandleAsync(integrationEvent, CancellationToken.None);
    }

    private async Task SeedActivePushDevice(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var device = PushDevice.Register(
            userId, PushProvider.Fcm, $"token-{Guid.NewGuid():N}",
            Guid.NewGuid().ToString("N"), "ios", "1.0", "ru-RU",
            DateTimeOffset.UtcNow);
        db.PushDevices.Add(device);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedNotification(Guid userId, DateTimeOffset? readAt)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notification = NotificationAggregate.Create(
            recipientUserId: userId,
            category: NotificationCategory.ChatMessageDirect,
            severity: NotificationSeverity.Normal,
            content: NotificationContent.Create("title", "body"),
            data: NotificationData.Empty,
            groupKey: null,
            sourceEventId: Guid.NewGuid(),
            sourceEventType: "test.v1",
            createdAtUtc: DateTimeOffset.UtcNow);
        if (readAt.HasValue)
        {
            notification.MarkRead(readAt.Value);
        }
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }
}
