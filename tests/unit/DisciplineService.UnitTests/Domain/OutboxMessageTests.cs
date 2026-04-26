using DisciplineService.Api.Domain.Aggregates;

namespace DisciplineService.UnitTests.Domain;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Create_PopulatesAllFields_AttemptsZero()
    {
        var id = Guid.NewGuid();
        var occurred = DateTimeOffset.UtcNow;

        var message = OutboxMessage.Create(id, "topic", "key", "{}", "evt.v1", occurred);

        Assert.Equal(id, message.Id);
        Assert.Equal("topic", message.Topic);
        Assert.Equal("key", message.Key);
        Assert.Equal("{}", message.Payload);
        Assert.Equal("evt.v1", message.EventType);
        Assert.Equal(occurred, message.OccurredAtUtc);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.PublishedAtUtc);
        Assert.Null(message.LastError);
        Assert.Null(message.NextAttemptAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankTopic(string topic)
    {
        Assert.Throws<ArgumentException>(() =>
            OutboxMessage.Create(Guid.NewGuid(), topic, "k", "{}", "e.v1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkPublished_StoresTimestampAndClearsError()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), "t", "k", "{}", "e.v1", DateTimeOffset.UtcNow);
        message.RecordFailure(DateTimeOffset.UtcNow, "boom", TimeSpan.FromSeconds(1));

        var publishedAt = DateTimeOffset.UtcNow;
        message.MarkPublished(publishedAt);

        Assert.Equal(publishedAt, message.PublishedAtUtc);
        Assert.Null(message.LastError);
        Assert.Null(message.NextAttemptAtUtc);
    }

    [Fact]
    public void RecordFailure_IncrementsAttemptsAndSchedulesRetry()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), "t", "k", "{}", "e.v1", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;

        message.RecordFailure(now, "kafka unavailable", TimeSpan.FromSeconds(5));

        Assert.Equal(1, message.Attempts);
        Assert.Equal("kafka unavailable", message.LastError);
        Assert.Equal(now + TimeSpan.FromSeconds(5), message.NextAttemptAtUtc);
    }

    [Fact]
    public void RecordFailure_TruncatesLongError()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), "t", "k", "{}", "e.v1", DateTimeOffset.UtcNow);
        var longError = new string('x', 2000);

        message.RecordFailure(DateTimeOffset.UtcNow, longError, TimeSpan.FromSeconds(1));

        Assert.Equal(1024, message.LastError!.Length);
    }
}
