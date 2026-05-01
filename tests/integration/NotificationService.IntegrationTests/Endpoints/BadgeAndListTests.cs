using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public sealed class BadgeAndListTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListNotifications_NoData_ReturnsEmptyArray()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<ListResponse>("/api/v1/me/notifications").ConfigureAwait(true);

        response!.Items.Should().BeEmpty();
        response.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ListNotifications_AfterInsert_ReturnsItem()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var notification = NotificationAggregate.Create(
                recipientUserId: userId,
                category: NotificationCategory.ChatMessageDirect,
                severity: NotificationSeverity.Normal,
                content: NotificationContent.Create("Hello", "World"),
                data: NotificationData.Empty,
                groupKey: null,
                sourceEventId: Guid.NewGuid(),
                sourceEventType: "test.v1",
                createdAtUtc: DateTimeOffset.UtcNow);

            db.Notifications.Add(notification);
            await db.SaveChangesAsync().ConfigureAwait(true);
        }

        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<ListResponse>("/api/v1/me/notifications").ConfigureAwait(true);

        response!.Items.Should().HaveCount(1);
        response.Items[0].Title.Should().Be("Hello");
        response.Items[0].Body.Should().Be("World");
    }

    [Fact]
    public async Task GetBadge_AfterIncrement_ReturnsTotal()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var badgeStore = scope.ServiceProvider.GetRequiredService<Urfu.Link.Services.Notification.Domain.Interfaces.IBadgeStore>();
            await badgeStore.IncrementAsync(userId, NotificationCategory.ChatMessageDirect, default).ConfigureAwait(true);
            await badgeStore.IncrementAsync(userId, NotificationCategory.CallMissed, default).ConfigureAwait(true);
        }

        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<BadgeSnapshotDto>("/api/v1/me/notifications/badge").ConfigureAwait(true);

        response!.Total.Should().Be(2);
    }

    private sealed record ListResponse(IReadOnlyList<NotificationDto> Items, string? NextCursor);
}
