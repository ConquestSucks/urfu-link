using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Notification.Application.Handlers.Call;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace NotificationService.UnitTests.Application;

public sealed class CallNotificationHandlerTests
{
    private static readonly Guid CallId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CallerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CalleeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherRecipientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset OccurredAt = new(2026, 5, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Incoming_call_notification_skips_caller_and_uses_urgent_call_metadata()
    {
        var evt = new CallIncomingEvent(
            CallId,
            CallerId,
            [CallerId, CalleeId, OtherRecipientId],
            CallType.Video,
            OccurredAt);

        var intents = await new CallIncomingHandler().PrepareAsync(evt, CancellationToken.None);

        intents.Select(intent => intent.RecipientUserId).Should().BeEquivalentTo([CalleeId, OtherRecipientId]);
        intents.Should().AllSatisfy(intent =>
        {
            intent.Category.Should().Be(NotificationCategory.CallIncoming);
            intent.Severity.Should().Be(NotificationSeverity.Urgent);
            intent.Priority.Should().Be(NotificationPriority.UrgentCall);
            intent.GroupKey.Should().Be(GroupKey.ForCall(CallId));
            intent.SourceActionId.Should().Be(NotificationSourceActions.CallIncoming(CallId));
            intent.Content.Title.Should().Be("Входящий звонок");
            intent.Content.Body.Should().Be("Видеозвонок от пользователя");
            intent.Content.DeepLink.Should().Be($"urfulink://call/{CallId:N}/incoming");
            intent.Data.Values["callId"].Should().Be(CallId.ToString("N"));
            intent.Data.Values["callerId"].Should().Be(CallerId.ToString("N"));
            intent.Data.Values["callType"].Should().Be(nameof(CallType.Video));
            intent.Actor.Should().NotBeNull();
            intent.Actor!.Id.Should().Be(CallerId);
        });
    }

    [Fact]
    public async Task Incoming_call_v2_notification_routes_to_source_chat()
    {
        const string conversationId = "direct-1";
        var evt = new CallIncomingV2Event(
            CallId,
            conversationId,
            CallerId,
            [CallerId, CalleeId, OtherRecipientId],
            CallType.Video,
            OccurredAt);

        var intents = await new CallIncomingHandler().PrepareAsync(evt, CancellationToken.None);

        intents.Select(intent => intent.RecipientUserId).Should().BeEquivalentTo([CalleeId, OtherRecipientId]);
        intents.Should().AllSatisfy(intent =>
        {
            intent.Content.DeepLink.Should().Be($"urfulink://chat/conv/{conversationId}");
            intent.Data.Values["conversationId"].Should().Be(conversationId);
            intent.Data.Values["callId"].Should().Be(CallId.ToString("N"));
            intent.Data.Values["callerId"].Should().Be(CallerId.ToString("N"));
            intent.Data.Values["callType"].Should().Be(nameof(CallType.Video));
        });
    }

    [Fact]
    public async Task Missed_call_notification_targets_recipient_and_includes_ring_seconds()
    {
        var evt = new CallMissedEvent(
            CallId,
            CallerId,
            CalleeId,
            CallType.Audio,
            TimeSpan.FromSeconds(47),
            OccurredAt);

        var intents = await new CallMissedHandler().PrepareAsync(evt, CancellationToken.None);

        var intent = intents.Should().ContainSingle().Subject;
        intent.RecipientUserId.Should().Be(CalleeId);
        intent.Category.Should().Be(NotificationCategory.CallMissed);
        intent.Severity.Should().Be(NotificationSeverity.Normal);
        intent.Priority.Should().Be(NotificationPriority.ChatMessage);
        intent.GroupKey.Should().Be(GroupKey.ForCall(CallId));
        intent.SourceActionId.Should().Be(NotificationSourceActions.CallMissed(CallId, CalleeId));
        intent.Content.Title.Should().Be("Пропущенный звонок");
        intent.Content.Body.Should().Be("Звонок остался без ответа");
        intent.Content.DeepLink.Should().Be($"urfulink://call/{CallId:N}/missed");
        intent.Data.Values["callId"].Should().Be(CallId.ToString("N"));
        intent.Data.Values["callerId"].Should().Be(CallerId.ToString("N"));
        intent.Data.Values["ringSeconds"].Should().Be("47");
        intent.Actor.Should().NotBeNull();
        intent.Actor!.Id.Should().Be(CallerId);
    }

    [Fact]
    public async Task Missed_call_v2_notification_routes_to_source_chat()
    {
        const string conversationId = "direct-1";
        var evt = new CallMissedV2Event(
            CallId,
            conversationId,
            CallerId,
            CalleeId,
            [CallerId, CalleeId],
            CallType.Audio,
            TimeSpan.FromSeconds(47),
            OccurredAt);

        var intents = await new CallMissedHandler().PrepareAsync(evt, CancellationToken.None);

        var intent = intents.Should().ContainSingle().Subject;
        intent.Content.DeepLink.Should().Be($"urfulink://chat/conv/{conversationId}");
        intent.Data.Values["conversationId"].Should().Be(conversationId);
        intent.Data.Values["callId"].Should().Be(CallId.ToString("N"));
        intent.Data.Values["callerId"].Should().Be(CallerId.ToString("N"));
        intent.Data.Values["ringSeconds"].Should().Be("47");
    }

    [Fact]
    public async Task Ended_call_archives_incoming_call_notification()
    {
        var repository = Substitute.For<INotificationRepository>();
        var badgeStore = Substitute.For<IBadgeStore>();
        var broadcaster = Substitute.For<INotificationBroadcaster>();
        var sourceActionId = NotificationSourceActions.CallIncoming(CallId);
        var archived = NotificationAggregate.Create(
            CalleeId,
            NotificationCategory.CallIncoming,
            NotificationSeverity.Urgent,
            NotificationContent.Create("Входящий звонок", "Звонок от пользователя"),
            NotificationData.Empty,
            GroupKey.ForCall(CallId),
            Guid.NewGuid(),
            "call.incoming.v2",
            OccurredAt,
            sourceActionId,
            NotificationPriority.UrgentCall);

        repository.ArchiveBySourceActionAsync(sourceActionId, OccurredAt, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<NotificationAggregate>>([archived]));
        repository.CountBadgeAsync(CalleeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new NotificationBadgeCounts(
                TotalUnread: 0,
                TotalUnseen: 0,
                UrgentUnread: 0,
                PerCategory: new Dictionary<NotificationCategory, int>(),
                PerType: new Dictionary<string, int>())));
        badgeStore.SetSnapshotAsync(CalleeId, Arg.Any<BadgeSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
        broadcaster.NotifyRemovedAsync(CalleeId, archived.Id, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        broadcaster.NotifyBadgeUpdatedAsync(CalleeId, Arg.Any<BadgeSnapshotDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new NotificationLifecycleService(repository, badgeStore, broadcaster);

        var affected = await service.ArchiveBySourceActionAsync(sourceActionId, OccurredAt, CancellationToken.None);

        affected.Should().Be(1);
        await repository.Received(1).ArchiveBySourceActionAsync(sourceActionId, OccurredAt, Arg.Any<CancellationToken>());
        await broadcaster.Received(1).NotifyRemovedAsync(CalleeId, archived.Id, Arg.Any<CancellationToken>());
        await badgeStore.Received(1).SetSnapshotAsync(CalleeId, Arg.Any<BadgeSnapshot>(), Arg.Any<CancellationToken>());
        await broadcaster.Received(1).NotifyBadgeUpdatedAsync(CalleeId, Arg.Any<BadgeSnapshotDto>(), Arg.Any<CancellationToken>());
    }
}
