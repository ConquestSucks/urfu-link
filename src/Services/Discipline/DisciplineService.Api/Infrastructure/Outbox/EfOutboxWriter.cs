using System.Text.Json;
using DisciplineService.Api.Infrastructure.Persistence;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using OutboxMessage = DisciplineService.Api.Domain.Aggregates.OutboxMessage;

namespace DisciplineService.Api.Infrastructure.Outbox;

/// <summary>
/// Transactional outbox writer: stages an <see cref="OutboxMessage"/> through the
/// scoped <see cref="DisciplineDbContext"/>. The row is persisted by the same
/// <c>SaveChangesAsync</c> call that flushes the domain change, so a process crash
/// after the DB commit still leaves the event ready for the relay worker. The
/// shared <see cref="IOutboxWriter"/> contract is preserved so existing repository
/// code in this service compiles unchanged.
/// </summary>
internal sealed class EfOutboxWriter(DisciplineDbContext dbContext) : IOutboxWriter
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public ValueTask EnqueueAsync<TEvent>(
        string topic,
        IntegrationEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(envelope, PayloadJsonOptions);
        var message = OutboxMessage.Create(
            messageId: envelope.MessageId,
            topic: topic,
            key: envelope.MessageId.ToString("N"),
            payload: payload,
            eventType: envelope.Payload.EventType,
            occurredAtUtc: envelope.Payload.OccurredAtUtc);

        dbContext.OutboxMessages.Add(message);
        return ValueTask.CompletedTask;
    }
}
