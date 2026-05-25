using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Events;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace NotificationService.UnitTests.Domain;

public sealed class NotificationTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly Guid SourceEventId = Guid.NewGuid();

    private static Notification NewNotification(
        NotificationCategory category = NotificationCategory.ChatMessageDirect,
        NotificationSeverity severity = NotificationSeverity.Normal,
        GroupKey? groupKey = null)
        => Notification.Create(
            recipientUserId: Recipient,
            category: category,
            severity: severity,
            content: NotificationContent.Create("Hello", "World"),
            data: NotificationData.Empty,
            groupKey: groupKey,
            sourceEventId: SourceEventId,
            sourceEventType: "chat.message.sent.v1",
            createdAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public void Create_AssignsIdentityAndDefaults()
    {
        var notification = NewNotification();

        notification.Id.Should().NotBe(Guid.Empty);
        notification.RecipientUserId.Should().Be(Recipient);
        notification.Category.Should().Be(NotificationCategory.ChatMessageDirect);
        notification.Severity.Should().Be(NotificationSeverity.Normal);
        notification.Content.Title.Should().Be("Hello");
        notification.SourceEventId.Should().Be(SourceEventId);
        notification.SourceEventType.Should().Be("chat.message.sent.v1");
        notification.ReadAtUtc.Should().BeNull();
        notification.GroupKey.Should().BeNull();
        notification.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public void Create_RaisesNotificationCreatedEvent()
    {
        var notification = NewNotification();

        notification.DomainEvents.Should().ContainSingle();
        var created = notification.DomainEvents[0].Should().BeOfType<NotificationCreatedEvent>().Subject;
        created.NotificationId.Should().Be(notification.Id);
        created.RecipientUserId.Should().Be(Recipient);
        created.Category.Should().Be(NotificationCategory.ChatMessageDirect);
    }

    [Fact]
    public void Create_RejectsEmptyRecipient()
    {
        var act = () => Notification.Create(
            recipientUserId: Guid.Empty,
            category: NotificationCategory.ChatMessageDirect,
            severity: NotificationSeverity.Normal,
            content: NotificationContent.Create("t", "b"),
            data: NotificationData.Empty,
            groupKey: null,
            sourceEventId: SourceEventId,
            sourceEventType: "chat.message.sent.v1",
            createdAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsEmptySourceEventId()
    {
        var act = () => Notification.Create(
            recipientUserId: Recipient,
            category: NotificationCategory.ChatMessageDirect,
            severity: NotificationSeverity.Normal,
            content: NotificationContent.Create("t", "b"),
            data: NotificationData.Empty,
            groupKey: null,
            sourceEventId: Guid.Empty,
            sourceEventType: "chat.message.sent.v1",
            createdAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsBlankSourceEventType()
    {
        var act = () => Notification.Create(
            recipientUserId: Recipient,
            category: NotificationCategory.ChatMessageDirect,
            severity: NotificationSeverity.Normal,
            content: NotificationContent.Create("t", "b"),
            data: NotificationData.Empty,
            groupKey: null,
            sourceEventId: SourceEventId,
            sourceEventType: "  ",
            createdAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_PreservesGroupKey()
    {
        var groupKey = GroupKey.ForDirectChat(Guid.NewGuid());

        var notification = NewNotification(groupKey: groupKey);

        notification.GroupKey.Should().Be(groupKey);
    }

    [Fact]
    public void MarkRead_SetsReadAtUtc_AndRaisesEvent()
    {
        var notification = NewNotification();
        notification.ClearDomainEvents();
        var readAt = DateTimeOffset.UtcNow.AddMinutes(5);

        var changed = notification.MarkRead(readAt);

        changed.Should().BeTrue();
        notification.ReadAtUtc.Should().Be(readAt);
        var evt = notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationReadEvent>().Subject;
        evt.NotificationId.Should().Be(notification.Id);
        evt.RecipientUserId.Should().Be(Recipient);
        evt.ReadAtUtc.Should().Be(readAt);
    }

    [Fact]
    public void MarkRead_IsIdempotent()
    {
        var notification = NewNotification();
        var first = DateTimeOffset.UtcNow;
        notification.MarkRead(first);
        notification.ClearDomainEvents();

        var changed = notification.MarkRead(first.AddMinutes(1));

        changed.Should().BeFalse();
        notification.ReadAtUtc.Should().Be(first);
        notification.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_AssignsEnterpriseDefaults()
    {
        var notification = NewNotification();

        notification.Type.Should().Be("chat.message.direct");
        notification.SeenAtUtc.Should().BeNull();
        notification.SavedAtUtc.Should().BeNull();
        notification.DoneAtUtc.Should().BeNull();
        notification.ArchivedAtUtc.Should().BeNull();
        notification.SnoozedUntilUtc.Should().BeNull();
        notification.ExpiresAtUtc.Should().BeNull();
        notification.OccurrenceCount.Should().Be(1);
        notification.LastOccurrenceAtUtc.Should().Be(notification.CreatedAtUtc);
        notification.Actions.Should().Contain(a => a.Id == "open");
    }

    [Fact]
    public void Create_AttachesContentDeepLinkToDefaultOpenAction()
    {
        const string deepLink = "urfulink://chat/conv/direct-1/msg/3f7d3e57b4f5481e93a1c8e9b4d70a11";

        var notification = Notification.Create(
            recipientUserId: Recipient,
            category: NotificationCategory.ChatMessageDirect,
            severity: NotificationSeverity.Normal,
            content: NotificationContent.Create("Hello", "World", deepLink: deepLink),
            data: NotificationData.Empty,
            groupKey: null,
            sourceEventId: SourceEventId,
            sourceEventType: "chat.message.sent.v1",
            createdAtUtc: DateTimeOffset.UtcNow);

        notification.Actions.Should().Contain(a =>
            a.Id == "open" &&
            a.Kind == "deep-link" &&
            a.DeepLink == deepLink);
    }

    [Fact]
    public void MarkSeen_SetsSeenAtOnce()
    {
        var notification = NewNotification();
        var first = DateTimeOffset.UtcNow;

        notification.MarkSeen(first).Should().BeTrue();
        notification.MarkSeen(first.AddMinutes(1)).Should().BeFalse();

        notification.SeenAtUtc.Should().Be(first);
    }

    [Fact]
    public void MarkUnread_ClearsReadStateButKeepsSeenState()
    {
        var notification = NewNotification();
        var seenAt = DateTimeOffset.UtcNow;
        notification.MarkSeen(seenAt);
        notification.MarkRead(seenAt.AddMinutes(1));

        notification.MarkUnread().Should().BeTrue();

        notification.ReadAtUtc.Should().BeNull();
        notification.SeenAtUtc.Should().Be(seenAt);
    }

    [Fact]
    public void SaveAndUnsave_AreIdempotent()
    {
        var notification = NewNotification();
        var savedAt = DateTimeOffset.UtcNow;

        notification.Save(savedAt).Should().BeTrue();
        notification.Save(savedAt.AddMinutes(1)).Should().BeFalse();
        notification.Unsave().Should().BeTrue();
        notification.Unsave().Should().BeFalse();

        notification.SavedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkDone_ReadsAndRemovesFromActiveInbox()
    {
        var notification = NewNotification();
        var doneAt = DateTimeOffset.UtcNow;

        notification.MarkDone(doneAt).Should().BeTrue();

        notification.DoneAtUtc.Should().Be(doneAt);
        notification.ReadAtUtc.Should().Be(doneAt);
        notification.SeenAtUtc.Should().Be(doneAt);
    }

    [Fact]
    public void Restore_ClearsDoneAndArchiveState()
    {
        var notification = NewNotification();
        notification.MarkDone(DateTimeOffset.UtcNow);

        notification.Restore().Should().BeTrue();

        notification.DoneAtUtc.Should().BeNull();
        notification.ArchivedAtUtc.Should().BeNull();
    }

    [Fact]
    public void AddDelivery_AppendsToCollection()
    {
        var notification = NewNotification();
        var delivery = Delivery.PendingInApp(notification.Id, $"user:{Recipient:N}");

        notification.AddDelivery(delivery);

        notification.Deliveries.Should().ContainSingle().Which.Should().BeSameAs(delivery);
    }

    [Fact]
    public void AddDelivery_RejectsForeignNotificationId()
    {
        var notification = NewNotification();
        var foreign = Delivery.PendingInApp(Guid.NewGuid(), $"user:{Recipient:N}");

        var act = () => notification.AddDelivery(foreign);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAll()
    {
        var notification = NewNotification();
        notification.DomainEvents.Should().NotBeEmpty();

        notification.ClearDomainEvents();

        notification.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_ImplementIIntegrationEvent()
    {
        var notification = NewNotification();

        notification.DomainEvents[0].Should().BeAssignableTo<IIntegrationEvent>();
    }
}
