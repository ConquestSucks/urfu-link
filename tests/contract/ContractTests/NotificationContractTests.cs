using System.Text.Json;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;
using Urfu.Link.BuildingBlocks.Contracts.Integration.User;

namespace ContractTests;

public sealed class NotificationContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void NotificationDelivered_RoundTripsThroughJson()
    {
        var original = new NotificationDeliveredEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Category: 1,
            Channel: 3,
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void NotificationRead_RoundTripsThroughJson()
    {
        var original = new NotificationReadEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void NotificationFailed_RoundTripsThroughJson()
    {
        var original = new NotificationFailedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Category: 11,
            Channel: 1,
            "fcm_quota_exceeded");

        AssertRoundTrip(original);
    }

    [Fact]
    public void CallIncoming_RoundTripsThroughJson()
    {
        var original = new CallIncomingEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            CallType.Video,
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void CallMissed_RoundTripsThroughJson()
    {
        var original = new CallMissedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CallType.Audio,
            TimeSpan.FromSeconds(45),
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void CallEnded_RoundTripsThroughJson()
    {
        var original = new CallEndedEvent(
            Guid.NewGuid(),
            TimeSpan.FromMinutes(3),
            CallEndReason.Completed,
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void UserNotificationSettingsChanged_RoundTripsThroughJson()
    {
        var prefs = new NotificationPreferencesPayload(
            new Dictionary<int, ChannelTogglePayload>
            {
                [1] = new(true, false, true),
                [2] = new(true, true, true),
            },
            new QuietHoursPayload("Asia/Yekaterinburg", "22:00", "08:00", true),
            DndEnabled: false,
            Locale: "ru-RU",
            MutedConversationIds: ["direct:abc", "discipline:def"]);

        var original = new UserNotificationSettingsChangedEvent(Guid.NewGuid(), prefs);

        AssertRoundTrip(original);
    }

    [Fact]
    public void UserDeleted_RoundTripsThroughJson()
    {
        var original = new UserDeletedEvent(Guid.NewGuid());
        AssertRoundTrip(original);
    }

    [Fact]
    public void UserRoleChanged_RoundTripsThroughJson()
    {
        var original = new UserRoleChangedEvent(Guid.NewGuid(), "Student", "Teacher");
        AssertRoundTrip(original);
    }

    [Fact]
    public void DisciplineAnnouncement_RoundTripsThroughJson()
    {
        var original = new DisciplineAnnouncementEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Контрольная перенесена",
            "Из-за карантина",
            new[] { Guid.NewGuid() });

        AssertRoundTrip(original);
    }

    [Fact]
    public void DisciplineMaterialPublished_RoundTripsThroughJson()
    {
        var original = new DisciplineMaterialPublishedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Lecture 5",
            "Slides + recording",
            new[] { Guid.NewGuid() });

        AssertRoundTrip(original);
    }

    [Fact]
    public void DisciplineDeadlineApproaching_RoundTripsThroughJson()
    {
        var original = new DisciplineDeadlineApproachingEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Lab 3",
            DateTimeOffset.UtcNow.AddDays(2),
            new[] { Guid.NewGuid() });

        AssertRoundTrip(original);
    }

    [Fact]
    public void ChatMessageSent_FromMovedNamespace_RoundTripsThroughJson()
    {
        var original = new ChatMessageSentEvent(
            "conv-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            "Hello",
            HasAttachments: false,
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void ChatMentionCreated_FromMovedNamespace_RoundTripsThroughJson()
    {
        var original = new ChatMentionCreatedEvent(
            "conv-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            DateTimeOffset.UtcNow);

        AssertRoundTrip(original);
    }

    [Fact]
    public void Envelope_PreservesPayload()
    {
        var payload = new UserDeletedEvent(Guid.NewGuid());
        var envelope = new IntegrationEnvelope<UserDeletedEvent>(
            Guid.NewGuid(),
            "trace-1",
            "user-service",
            DateTimeOffset.UtcNow,
            payload);

        var json = JsonSerializer.Serialize(envelope, Json);
        var roundTripped = JsonSerializer.Deserialize<IntegrationEnvelope<UserDeletedEvent>>(json, Json);

        Assert.NotNull(roundTripped);
        Assert.Equal(envelope.MessageId, roundTripped!.MessageId);
        Assert.Equal(envelope.TraceId, roundTripped.TraceId);
        Assert.Equal(payload.UserId, roundTripped.Payload.UserId);
    }

    private static void AssertRoundTrip<T>(T original)
        where T : IIntegrationEvent
    {
        var json = JsonSerializer.Serialize(original, Json);
        var roundTripped = JsonSerializer.Deserialize<T>(json, Json);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.EventType, roundTripped!.EventType);
    }
}
