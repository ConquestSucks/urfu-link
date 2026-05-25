using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace NotificationService.IntegrationTests.Routing;

/// <summary>
/// End-to-end test of the presence-aware push skip rule. The router asks the
/// gRPC presence client whether the recipient is currently online on web; if
/// so, push deliveries for chat categories are suppressed (the user is already
/// receiving the in-app SignalR push). Calls and urgent severity bypass the
/// skip — those must always reach the device.
///
/// Asserts the rule end-to-end through <c>NotificationRouter</c>, including
/// the actual EF write to <c>notifications.deliveries</c>, so a regression in
/// the wiring (e.g. a refactor that re-introduces the Offline stub by accident)
/// would surface here.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class PresenceAwareRoutingTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    private readonly NotificationServiceFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Skips_push_for_chat_message_when_user_online_on_web()
    {
        var userId = Guid.NewGuid();
        await SeedActivePushDevice(userId);
        _factory.PresenceClient.OnlineOnWeb = true;

        var draft = ChatDraft(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Normal);

        var outcome = await RouteAsync(draft);

        outcome.Created.Should().Be(1);
        var deliveries = await ListDeliveriesAsync(userId);
        deliveries.Should().NotBeEmpty();
        deliveries.Should().NotContain(d => d.Channel == DeliveryChannel.Push,
            "the user is already receiving the chat message in-app on web — push would duplicate.");
        deliveries.Should().Contain(d => d.Channel == DeliveryChannel.InApp);
    }

    [Fact]
    public async Task Delivers_push_for_chat_message_when_user_offline()
    {
        var userId = Guid.NewGuid();
        await SeedActivePushDevice(userId);
        _factory.PresenceClient.OnlineOnWeb = false;

        var draft = ChatDraft(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Normal);

        var outcome = await RouteAsync(draft);

        outcome.Created.Should().Be(1);
        var deliveries = await ListDeliveriesAsync(userId);
        deliveries.Should().Contain(d => d.Channel == DeliveryChannel.Push,
            "presence is offline — push must reach the recipient.");
    }

    [Fact]
    public async Task Delivers_push_for_call_incoming_even_when_user_online_on_web()
    {
        var userId = Guid.NewGuid();
        await SeedActivePushDevice(userId);
        _factory.PresenceClient.OnlineOnWeb = true;

        // Calls bypass presence-aware skip — incoming calls must ring on the device
        // even when the user is browsing on web.
        var draft = ChatDraft(userId, NotificationCategory.CallIncoming, NotificationSeverity.High);

        var outcome = await RouteAsync(draft);

        outcome.Created.Should().Be(1);
        var deliveries = await ListDeliveriesAsync(userId);
        deliveries.Should().Contain(d => d.Channel == DeliveryChannel.Push,
            "call categories never skip push — the device must ring.");
    }

    [Fact]
    public async Task Delivers_push_for_urgent_severity_even_when_user_online_on_web()
    {
        var userId = Guid.NewGuid();
        await SeedActivePushDevice(userId);
        _factory.PresenceClient.OnlineOnWeb = true;

        // Urgent severity bypasses presence-aware skip even for chat categories.
        var draft = ChatDraft(userId, NotificationCategory.ChatMessageDirect, NotificationSeverity.Urgent);

        var outcome = await RouteAsync(draft);

        outcome.Created.Should().Be(1);
        var deliveries = await ListDeliveriesAsync(userId);
        deliveries.Should().Contain(d => d.Channel == DeliveryChannel.Push,
            "urgent severity always reaches the device regardless of web presence.");
    }

    private async Task<RoutingOutcome> RouteAsync(NotificationIntent draft)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<NotificationRouter>();
        return await router.RouteAsync(new TestEvent(), new StaticHandler<TestEvent>([draft]), CancellationToken.None);
    }

    private async Task<List<Delivery>> ListDeliveriesAsync(Guid recipientUserId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notificationIds = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId)
            .Select(n => n.Id)
            .ToListAsync();
        return await db.Deliveries.AsNoTracking()
            .Where(d => notificationIds.Contains(d.NotificationId))
            .ToListAsync();
    }

    private async Task SeedActivePushDevice(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var device = PushDevice.Register(
            userId: userId,
            provider: PushProvider.Fcm,
            token: $"token-{userId:N}",
            deviceFingerprint: Guid.NewGuid().ToString("N"),
            platform: "ios",
            appVersion: "1.0",
            locale: "en-US",
            registeredAtUtc: DateTimeOffset.UtcNow);
        db.PushDevices.Add(device);
        await db.SaveChangesAsync();
    }

    private static NotificationIntent ChatDraft(Guid userId, NotificationCategory category, NotificationSeverity severity) =>
        new(
            RecipientUserId: userId,
            Category: category,
            Severity: severity,
            Content: NotificationContent.Create("title", "body"),
            Data: NotificationData.Empty,
            GroupKey: null,
            SourceEventId: Guid.NewGuid(),
            SourceEventType: "test.event.v1");

    private sealed record TestEvent : IIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public string EventType => "test.event.v1";
        public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    }

    private sealed class StaticHandler<TEvent>(NotificationIntent[] drafts) : INotificationHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(TEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotificationIntent>>(drafts);
    }
}
