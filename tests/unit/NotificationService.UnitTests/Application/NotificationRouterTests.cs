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
using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.UnitTests.Application;

public sealed class NotificationRouterTests
{
    private readonly IUserPreferencesClient _prefs = Substitute.For<IUserPreferencesClient>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IBadgeStore _badgeStore = Substitute.For<IBadgeStore>();
    private readonly INotificationBroadcaster _broadcaster = Substitute.For<INotificationBroadcaster>();
    private readonly NotificationFactory _factory = new(TimeProvider.System);
    private readonly NotificationRouter _router;

    public NotificationRouterTests()
    {
        _prefs.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UserPreferences.Default);
        _prefs.GetContactAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UserContact("user@urfu.ru", "User", "ru-RU"));
        _repository.TryInsertAsync(Arg.Any<NotificationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _badgeStore.GetSnapshotAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BadgeSnapshot.Empty);

        var inAppChannel = new InAppChannel(_broadcaster, new BadgeService(_badgeStore));

        _router = new NotificationRouter(
            _prefs,
            _repository,
            _factory,
            TimeProvider.System,
            _badgeStore,
            inAppChannel,
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

        var outcome = await _router.RouteAsync(evt, new ChatMessageSentHandler(), default);

        outcome.Should().Be(RoutingOutcome.NoDrafts);
        await _repository.DidNotReceive().TryInsertAsync(Arg.Any<NotificationAggregate>(), Arg.Any<CancellationToken>());
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

        var outcome = await _router.RouteAsync(evt, new ChatMessageSentHandler(), default);

        outcome.Created.Should().Be(1);
        outcome.Skipped.Should().Be(0);

        await _repository.Received(1).TryInsertAsync(
            Arg.Is<NotificationAggregate>(n => n.RecipientUserId == bob && n.Category == NotificationCategory.ChatMessageDirect),
            Arg.Any<CancellationToken>());
        await _badgeStore.Received(1).IncrementAsync(bob, NotificationCategory.ChatMessageDirect, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_DuplicateInsert_CountsSkippedAndDoesNotIncrementBadge()
    {
        _repository.TryInsertAsync(Arg.Any<NotificationAggregate>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipients: [Guid.NewGuid()],
            "Hi",
            false,
            DateTimeOffset.UtcNow);

        var outcome = await _router.RouteAsync(evt, new ChatMessageSentHandler(), default);

        outcome.Created.Should().Be(0);
        outcome.Skipped.Should().Be(1);
        await _badgeStore.DidNotReceive().IncrementAsync(Arg.Any<Guid>(), Arg.Any<NotificationCategory>(), Arg.Any<CancellationToken>());
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
        _repository.TryInsertAsync(Arg.Do<NotificationAggregate>(n => captured = n), Arg.Any<CancellationToken>())
            .Returns(true);

        var evt = new ChatMessageSentEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Recipients: [Guid.NewGuid()],
            "Hi",
            false,
            DateTimeOffset.UtcNow);

        await _router.RouteAsync(evt, new ChatMessageSentHandler(), default);

        captured.Should().NotBeNull();
        captured!.Deliveries.Should().BeEmpty();
    }
}
