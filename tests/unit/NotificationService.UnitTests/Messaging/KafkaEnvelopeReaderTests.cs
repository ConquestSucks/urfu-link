using System.Text.Json;
using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Messaging;

namespace NotificationService.UnitTests.Messaging;

public sealed class KafkaEnvelopeReaderTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Read_AcceptsDefaultSerializedIntegrationEnvelope()
    {
        var envelope = CreateEnvelope();

        var parsed = KafkaEnvelopeReader.Read(JsonSerializer.Serialize(envelope));

        parsed.MessageId.Should().Be(envelope.MessageId.ToString("D"));
        parsed.EventType.Should().Be("chat.message.sent.v1");
        parsed.Payload["ConversationId"]!.GetValue<string>().Should().Be("direct-1");
        parsed.Payload.Deserialize<ChatMessageSentEvent>(WebJsonOptions)!.ConversationId.Should().Be("direct-1");
    }

    [Fact]
    public void Read_AcceptsWebSerializedIntegrationEnvelope()
    {
        var envelope = CreateEnvelope();

        var parsed = KafkaEnvelopeReader.Read(JsonSerializer.Serialize(envelope, WebJsonOptions));

        parsed.MessageId.Should().Be(envelope.MessageId.ToString("D"));
        parsed.EventType.Should().Be("chat.message.sent.v1");
        parsed.Payload["conversationId"]!.GetValue<string>().Should().Be("direct-1");
    }

    private static IntegrationEnvelope<ChatMessageSentEvent> CreateEnvelope()
    {
        var payload = new ChatMessageSentEvent(
            ConversationId: "direct-1",
            MessageId: Guid.NewGuid(),
            SenderId: Guid.NewGuid(),
            Recipients: [Guid.NewGuid()],
            Preview: "hello",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        return new IntegrationEnvelope<ChatMessageSentEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: "trace",
            Source: "chat-service",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: payload);
    }
}
