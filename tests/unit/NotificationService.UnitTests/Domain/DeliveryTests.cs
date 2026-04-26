using FluentAssertions;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Domain;

public sealed class DeliveryTests
{
    private static readonly Guid NotificationId = Guid.NewGuid();
    private static readonly Guid PushDeviceId = Guid.NewGuid();

    [Fact]
    public void PendingPush_InitializesAddressAndProvider()
    {
        var delivery = Delivery.PendingPush(NotificationId, PushProvider.Fcm, PushDeviceId, " token-123 ");

        delivery.Channel.Should().Be(DeliveryChannel.Push);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.Address.Should().Be("token-123");
        delivery.Provider.Should().Be(PushProvider.Fcm);
        delivery.PushDeviceId.Should().Be(PushDeviceId);
        delivery.NotificationId.Should().Be(NotificationId);
        delivery.Attempts.Should().Be(0);
    }

    [Fact]
    public void PendingEmail_InitializesAddress()
    {
        var delivery = Delivery.PendingEmail(NotificationId, " user@urfu.ru ");

        delivery.Channel.Should().Be(DeliveryChannel.Email);
        delivery.Address.Should().Be("user@urfu.ru");
        delivery.Provider.Should().BeNull();
    }

    [Fact]
    public void PendingInApp_UsesUserScopedAddress()
    {
        var delivery = Delivery.PendingInApp(NotificationId, "user:abc");

        delivery.Channel.Should().Be(DeliveryChannel.InApp);
        delivery.Address.Should().Be("user:abc");
    }

    [Fact]
    public void MarkSent_UpdatesStateAndRecordsAttempt()
    {
        var delivery = Delivery.PendingInApp(NotificationId, "user:abc");
        var now = DateTimeOffset.UtcNow;

        delivery.MarkSent(now, providerMessageId: "msg-1");

        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.Attempts.Should().Be(1);
        delivery.LastAttemptAtUtc.Should().Be(now);
        delivery.CompletedAtUtc.Should().Be(now);
        delivery.NextAttemptAtUtc.Should().BeNull();
        delivery.ProviderMessageId.Should().Be("msg-1");
    }

    [Fact]
    public void MarkSent_ThrowsWhenAlreadyCompleted()
    {
        var delivery = Delivery.PendingInApp(NotificationId, "user:abc");
        delivery.MarkSkipped(DateTimeOffset.UtcNow, "preference");

        var act = () => delivery.MarkSent(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RecordFailure_SchedulesNextAttempt_AndKeepsPending()
    {
        var delivery = Delivery.PendingPush(NotificationId, PushProvider.Fcm, PushDeviceId, "token");
        var now = DateTimeOffset.UtcNow;

        delivery.RecordFailure(now, "transient", TimeSpan.FromMinutes(1));

        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.Attempts.Should().Be(1);
        delivery.LastError.Should().Be("transient");
        delivery.NextAttemptAtUtc.Should().Be(now + TimeSpan.FromMinutes(1));
        delivery.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkFinalFailed_SealsAsFailed()
    {
        var delivery = Delivery.PendingPush(NotificationId, PushProvider.Fcm, PushDeviceId, "token");
        delivery.RecordFailure(DateTimeOffset.UtcNow, "transient", TimeSpan.FromMinutes(1));
        var now = DateTimeOffset.UtcNow.AddMinutes(2);

        delivery.MarkFinalFailed(now, "max retries");

        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.CompletedAtUtc.Should().Be(now);
        delivery.LastError.Should().Be("max retries");
        delivery.NextAttemptAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkSkipped_RecordsReason()
    {
        var delivery = Delivery.PendingEmail(NotificationId, "x@y.com");
        var now = DateTimeOffset.UtcNow;

        delivery.MarkSkipped(now, "preference_disabled");

        delivery.Status.Should().Be(DeliveryStatus.Skipped);
        delivery.SkipReason.Should().Be("preference_disabled");
        delivery.CompletedAtUtc.Should().Be(now);
    }

    [Fact]
    public void MarkDelivered_TransitionsFromSentToDelivered()
    {
        var delivery = Delivery.PendingInApp(NotificationId, "user:abc");
        var now = DateTimeOffset.UtcNow;
        delivery.MarkSent(now);

        delivery.MarkDelivered(now.AddSeconds(1));

        delivery.Status.Should().Be(DeliveryStatus.Delivered);
    }

    [Fact]
    public void MarkDelivered_RejectsPendingStatus()
    {
        var delivery = Delivery.PendingInApp(NotificationId, "user:abc");

        var act = () => delivery.MarkDelivered(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PendingPush_RejectsBlankToken()
    {
        var act = () => Delivery.PendingPush(NotificationId, PushProvider.Fcm, PushDeviceId, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Pending_RejectsEmptyNotificationId()
    {
        var act = () => Delivery.PendingInApp(Guid.Empty, "user:abc");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordFailure_TruncatesOversizedError()
    {
        var delivery = Delivery.PendingEmail(NotificationId, "x@y.com");
        var hugeError = new string('e', Delivery.ErrorMaxLength + 100);

        delivery.RecordFailure(DateTimeOffset.UtcNow, hugeError, null);

        delivery.LastError.Should().HaveLength(Delivery.ErrorMaxLength);
    }
}
