using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Outbox;
using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.UnitTests.Application;

public sealed class NotificationRouterTests
{
    private readonly IUserPreferencesClient _prefs = Substitute.For<IUserPreferencesClient>();
    private readonly IPresenceClient _presence = Substitute.For<IPresenceClient>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IPushDeviceRepository _pushDevices = Substitute.For<IPushDeviceRepository>();
    private readonly IBadgeStore _badgeStore = Substitute.For<IBadgeStore>();
    private readonly INotificationBroadcaster _broadcaster = Substitute.For<INotificationBroadcaster>();
    private readonly IOutboxEnqueue _outboxEnqueue = Substitute.For<IOutboxEnqueue>();
    private readonly NotificationFactory _factory = new(TimeProvider.System);
    private readonly NotificationRouter _router;

    public NotificationRouterTests()
    {
        _prefs.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UserPreferences.Default);
        _prefs.GetContactAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UserContact("user@urfu.ru", "User", "ru-RU"));
        _presence.IsOnlineOnWebAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _presence.IsViewingAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.UpsertAsync(Arg.Any<NotificationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(call => NotificationUpsertResult.Created(call.Arg<NotificationAggregate>()));
        _pushDevices.ListActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Urfu.Link.Services.Notification.Domain.Aggregates.PushDevice>());
        _badgeStore.GetSnapshotAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BadgeSnapshot.Empty);

        var inAppChannel = new InAppChannel(_broadcaster, new BadgeService(_badgeStore));

        _router = new NotificationRouter(
            _prefs,
            _presence,
            _repository,
            _pushDevices,
            _factory,
            TimeProvider.System,
            _badgeStore,
            inAppChannel,
            _outboxEnqueue,
            NullLogger<NotificationRouter>.Instance);
    }

    [Fact]
    public async Task RouteAsync_NoDrafts_ReturnsEmptyOutcome()
    {
        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipients: [],
            "Hi",
            false,
            DateTimeOffset.UtcNow);

        var outcome = await _router.RouteAsync(evt, new ChatMessageSentHandler(Substitute.For<IDisciplineConversationLookup>()), default);

        outcome.Should().Be(RoutingOutcome.NoDrafts);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<NotificationAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_OneRecipient_PersistsAndIncrementsBadge()
    {
        var sender = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            sender,
            Recipients: [bob],
            "Hi",
            false,
            DateTimeOffset.UtcNow);

        var outcome = await _router.RouteAsync(evt, new ChatMessageSentHandler(Substitute.For<IDisciplineConversationLookup>()), default);

        outcome.Created.Should().Be(1);
        outcome.Skipped.Should().Be(0);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<NotificationAggregate>(n => n.RecipientUserId == bob && n.Category == NotificationCategory.ChatMessageDirect),
            Arg.Any<CancellationToken>());
        await _badgeStore.Received(1).IncrementAsync(bob, NotificationCategory.ChatMessageDirect, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_ResolvesActorDisplayNameBeforePersisting()
    {
        var sender = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _prefs.GetContactAsync(sender, Arg.Any<CancellationToken>())
            .Returns(new UserContact(string.Empty, "Иван Петров", "ru-RU"));
        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            sender,
            Recipients: [bob],
            "Привет",
            false,
            DateTimeOffset.UtcNow);

        await _router.RouteAsync(evt, new ChatMessageSentHandler(Substitute.For<IDisciplineConversationLookup>()), default);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<NotificationAggregate>(n =>
                n.Actor != null &&
                n.Actor.Id == sender &&
                n.Actor.DisplayName == "Иван Петров"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_DuplicateInsert_CountsSkippedAndDoesNotIncrementBadge()
    {
        _repository.UpsertAsync(Arg.Any<NotificationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(NotificationUpsertResult.Skipped());

        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipients: [Guid.NewGuid()],
            "Hi",
            false,
            DateTimeOffset.UtcNow);

        var outcome = await _router.RouteAsync(evt, new ChatMessageSentHandler(Substitute.For<IDisciplineConversationLookup>()), default);

        outcome.Created.Should().Be(0);
        outcome.Skipped.Should().Be(1);
        await _badgeStore.DidNotReceive().IncrementAsync(Arg.Any<Guid>(), Arg.Any<NotificationCategory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_RecipientAlreadyViewingContext_SkipsBellItem()
    {
        var recipient = Guid.NewGuid();
        const string viewingContext = "chat:conversation:direct-1";
        _presence.IsViewingAsync(recipient, viewingContext, Arg.Any<CancellationToken>())
            .Returns(true);

        var intent = new NotificationIntent(
            RecipientUserId: recipient,
            Category: NotificationCategory.ChatMessageDirect,
            Severity: NotificationSeverity.Normal,
            Content: Urfu.Link.Services.Notification.Domain.ValueObjects.NotificationContent.Create("title", "body"),
            Data: Urfu.Link.Services.Notification.Domain.ValueObjects.NotificationData.Empty,
            GroupKey: null,
            SourceEventId: Guid.NewGuid(),
            SourceEventType: "test.event.v1",
            SourceActionId: "chat:message:direct-1:00000000000000000000000000000001",
            Priority: NotificationPriority.ChatMessage,
            SuppressWhenViewingContextKey: viewingContext);

        var outcome = await _router.RouteAsync(
            new TestEvent(),
            new StaticHandler<TestEvent>([intent]),
            default);

        outcome.Created.Should().Be(0);
        outcome.Updated.Should().Be(0);
        outcome.Skipped.Should().Be(1);
        await _repository.DidNotReceive().UpsertAsync(
            Arg.Any<NotificationAggregate>(),
            Arg.Any<CancellationToken>());
        await _badgeStore.DidNotReceive().IncrementAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationCategory>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_PreferencesDisableInApp_NoInAppDelivery()
    {
        _prefs.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UserPreferences.Default with
            {
                Categories = new Dictionary<NotificationCategory, ChannelToggle>
                {
                    [NotificationCategory.ChatMessageDirect] = new(false, false, false),
                },
            });

        NotificationAggregate? captured = null;
        _repository.UpsertAsync(Arg.Do<NotificationAggregate>(n => captured = n), Arg.Any<CancellationToken>())
            .Returns(call => NotificationUpsertResult.Created(call.Arg<NotificationAggregate>()));

        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipients: [Guid.NewGuid()],
            "Hi",
            false,
            DateTimeOffset.UtcNow);

        await _router.RouteAsync(evt, new ChatMessageSentHandler(Substitute.For<IDisciplineConversationLookup>()), default);

        captured.Should().NotBeNull();
        captured!.Deliveries.Should().BeEmpty();
    }

    private sealed record TestEvent : Urfu.Link.BuildingBlocks.Contracts.Integration.IIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public string EventType => "test.event.v1";
        public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
    }

    private sealed class StaticHandler<TEvent>(IReadOnlyList<NotificationIntent> intents) : INotificationHandler<TEvent>
        where TEvent : Urfu.Link.BuildingBlocks.Contracts.Integration.IIntegrationEvent
    {
        public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(TEvent integrationEvent, CancellationToken cancellationToken)
        {
            _ = integrationEvent;
            _ = cancellationToken;
            return Task.FromResult(intents);
        }
    }
}
