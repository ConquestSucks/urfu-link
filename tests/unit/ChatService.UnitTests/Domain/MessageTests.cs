using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class MessageTests
{
    private const string ConversationId = "abc";
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Created = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);

    private static Message NewMessage() => Message.Send(
        id: Guid.NewGuid(),
        conversationId: ConversationId,
        senderId: Sender,
        body: "hello",
        attachments: Array.Empty<Attachment>(),
        clientMessageId: "client-1",
        createdAtUtc: Created);

    [Fact]
    public void Send_StartsInSentState_WithNoDeliveryTimestamps()
    {
        var message = NewMessage();

        message.State.Should().Be(MessageState.Sent);
        message.DeliveredAtUtc.Should().BeNull();
        message.ReadAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkDelivered_FromSent_TransitionsToDelivered()
    {
        var message = NewMessage();
        var deliveredAt = Created.AddSeconds(2);

        message.MarkDelivered(deliveredAt);

        message.State.Should().Be(MessageState.Delivered);
        message.DeliveredAtUtc.Should().Be(deliveredAt);
        message.ReadAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkDelivered_IsIdempotent()
    {
        var message = NewMessage();
        var firstAt = Created.AddSeconds(2);
        var secondAt = Created.AddSeconds(5);

        message.MarkDelivered(firstAt);
        message.MarkDelivered(secondAt);

        message.DeliveredAtUtc.Should().Be(firstAt);
    }

    [Fact]
    public void MarkRead_FromDelivered_TransitionsToRead()
    {
        var message = NewMessage();
        message.MarkDelivered(Created.AddSeconds(2));
        var readAt = Created.AddSeconds(10);

        message.MarkRead(readAt);

        message.State.Should().Be(MessageState.Read);
        message.ReadAtUtc.Should().Be(readAt);
        message.DeliveredAtUtc.Should().Be(Created.AddSeconds(2));
    }

    [Fact]
    public void MarkRead_FromSent_AlsoSetsDeliveredTimestamp()
    {
        var message = NewMessage();
        var readAt = Created.AddSeconds(10);

        message.MarkRead(readAt);

        message.State.Should().Be(MessageState.Read);
        message.DeliveredAtUtc.Should().Be(readAt);
        message.ReadAtUtc.Should().Be(readAt);
    }

    [Fact]
    public void MarkRead_IsIdempotent()
    {
        var message = NewMessage();
        var firstReadAt = Created.AddSeconds(10);
        message.MarkRead(firstReadAt);

        message.MarkRead(Created.AddSeconds(20));

        message.ReadAtUtc.Should().Be(firstReadAt);
    }
}
