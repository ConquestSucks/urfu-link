using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed record KafkaEnvelopeReadResult(
    string MessageId,
    string EventType,
    JsonNode Payload);

public static class KafkaEnvelopeReader
{
    public static KafkaEnvelopeReadResult Read(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var envelope = JsonNode.Parse(raw)
            ?? throw new JsonException("Envelope is null");

        var messageId = ReadStringOrThrow(envelope, "messageId", "MessageId");
        var payloadNode = ReadNodeOrThrow(envelope, "payload", "Payload");
        var eventType = ReadStringOrThrow(payloadNode, "eventType", "EventType");

        return new KafkaEnvelopeReadResult(messageId, eventType, payloadNode);
    }

    private static JsonNode ReadNodeOrThrow(JsonNode node, string camelCaseProperty, string pascalCaseProperty)
    {
        var value = node[camelCaseProperty] ?? node[pascalCaseProperty];
        if (value is null)
        {
            throw new JsonException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Missing '{camelCaseProperty}'/'{pascalCaseProperty}' on Kafka envelope."));
        }

        return value;
    }

    private static string ReadStringOrThrow(JsonNode node, string camelCaseProperty, string pascalCaseProperty)
    {
        var value = node[camelCaseProperty]?.GetValue<string>()
            ?? node[pascalCaseProperty]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Missing or empty '{camelCaseProperty}'/'{pascalCaseProperty}' on Kafka envelope."));
        }

        return value;
    }
}
