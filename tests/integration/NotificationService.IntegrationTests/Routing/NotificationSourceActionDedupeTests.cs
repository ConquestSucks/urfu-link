using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace NotificationService.IntegrationTests.Routing;

[Collection(IntegrationCollection.Name)]
public sealed class NotificationSourceActionDedupeTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DuplicateEventDelivery_CreatesOneNotification()
    {
        var userId = Guid.NewGuid();
        var intent = Intent(
            userId,
            NotificationCategory.ChatMessageDirect,
            NotificationPriority.ChatMessage,
            "chat.message.sent.v1",
            sourceActionId: $"chat:message:{Guid.NewGuid()}:{Guid.NewGuid():N}",
            sourceEventId: Guid.NewGuid());

        var first = await RouteAsync(intent);
        var second = await RouteAsync(intent);

        first.Created.Should().Be(1);
        second.Skipped.Should().Be(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var count = await db.Notifications.CountAsync(n => n.RecipientUserId == userId).ConfigureAwait(true);
        count.Should().Be(1);
    }

    [Fact]
    public async Task LateMention_UpgradesExistingMessageNotificationAndKeepsBadgeAtOne()
    {
        var userId = Guid.NewGuid();
        var sourceActionId = $"chat:message:{Guid.NewGuid()}:{Guid.NewGuid():N}";
        var direct = Intent(
            userId,
            NotificationCategory.ChatMessageDirect,
            NotificationPriority.ChatMessage,
            "chat.message.sent.v1",
            sourceActionId,
            Guid.NewGuid());
        var mention = Intent(
            userId,
            NotificationCategory.ChatMessageMention,
            NotificationPriority.Mention,
            "chat.mention.created.v1",
            sourceActionId,
            Guid.NewGuid());

        var created = await RouteAsync(direct);
        var upgraded = await RouteAsync(mention);

        created.Created.Should().Be(1);
        upgraded.Updated.Should().Be(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notification = await db.Notifications.SingleAsync(n => n.RecipientUserId == userId).ConfigureAwait(true);
        notification.Category.Should().Be(NotificationCategory.ChatMessageMention);
        notification.Priority.Should().Be(NotificationPriority.Mention);
        notification.SourceActionId.Should().Be(sourceActionId);

        var badgeStore = scope.ServiceProvider.GetRequiredService<IBadgeStore>();
        var badge = await badgeStore.GetSnapshotAsync(userId, CancellationToken.None).ConfigureAwait(true);
        badge.Total.Should().Be(1);
        badge.PerCategory.Should().ContainKey(NotificationCategory.ChatMessageMention);
        badge.PerCategory.Should().NotContainKey(NotificationCategory.ChatMessageDirect);
    }

    private async Task<RoutingOutcome> RouteAsync(NotificationIntent intent)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<NotificationRouter>();
        return await router.RouteAsync(new TestEvent(), new StaticHandler<TestEvent>([intent]), CancellationToken.None)
            .ConfigureAwait(true);
    }

    private static NotificationIntent Intent(
        Guid userId,
        NotificationCategory category,
        NotificationPriority priority,
        string eventType,
        string sourceActionId,
        Guid sourceEventId)
        => new(
            RecipientUserId: userId,
            Category: category,
            Severity: category == NotificationCategory.ChatMessageMention ? NotificationSeverity.High : NotificationSeverity.Normal,
            Content: NotificationContent.Create(category.ToString(), "body"),
            Data: NotificationData.Empty,
            GroupKey: null,
            SourceEventId: sourceEventId,
            SourceEventType: eventType,
            SourceActionId: sourceActionId,
            Priority: priority);

    private sealed record TestEvent : IIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public string EventType => "test.event.v1";
        public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    }

    private sealed class StaticHandler<TEvent>(NotificationIntent[] intents) : INotificationHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(TEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotificationIntent>>(intents);
    }
}
