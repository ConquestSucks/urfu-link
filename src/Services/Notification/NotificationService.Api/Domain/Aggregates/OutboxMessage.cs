namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    public string Topic { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Enqueue(
        string topic,
        string eventType,
        string payload,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = topic.Trim(),
            EventType = eventType.Trim(),
            Payload = payload,
            CreatedAtUtc = nowUtc,
            NextAttemptAtUtc = nowUtc,
            Attempts = 0,
        };
    }

    public void MarkPublished(DateTimeOffset publishedAtUtc)
    {
        PublishedAtUtc = publishedAtUtc;
        LastError = null;
    }

    public void RecordFailure(DateTimeOffset attemptAtUtc, string error, TimeSpan backoff)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Attempts++;
        LastError = error.Length > 1024 ? error[..1024] : error;
        NextAttemptAtUtc = attemptAtUtc + backoff;
    }
}
