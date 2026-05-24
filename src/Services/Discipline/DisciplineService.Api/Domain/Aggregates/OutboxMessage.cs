namespace DisciplineService.Api.Domain.Aggregates;

/// <summary>
/// Persisted integration event awaiting publication to Kafka. Written inside the same
/// EF Core SaveChanges transaction as the discipline aggregate, so the outbox row and
/// the domain change either both commit or both roll back. A dedicated relay worker
/// later reads unsent rows and produces them to the broker; this avoids the lost-event
/// window of the legacy Redis-based outbox where the DB commit could succeed before
/// the Redis enqueue ran.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    public string Topic { get; private set; } = null!;

    public string Key { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(
        Guid messageId,
        string topic,
        string key,
        string payload,
        string eventType,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return new OutboxMessage
        {
            Id = messageId,
            Topic = topic,
            Key = key,
            Payload = payload,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            Attempts = 0,
        };
    }

    public void MarkPublished(DateTimeOffset nowUtc)
    {
        PublishedAtUtc = nowUtc;
        LastError = null;
        NextAttemptAtUtc = null;
    }

    public void RecordFailure(DateTimeOffset nowUtc, string error, TimeSpan backoff)
    {
        ArgumentNullException.ThrowIfNull(error);
        Attempts += 1;
        LastError = error.Length > 1024 ? error[..1024] : error;
        NextAttemptAtUtc = nowUtc + backoff;
    }
}
