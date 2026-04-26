using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class MessageReadByTests
{
    private const string ConversationId = "abc";
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reader1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Reader2 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Created = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(48);

    private static Message NewMessage() => Message.Send(
        id: Guid.NewGuid(),
        conversationId: ConversationId,
        senderId: Sender,
        body: "hi",
        attachments: Array.Empty<Attachment>(),
        clientMessageId: "client-1",
        createdAtUtc: Created);

    [Fact]
    public void MarkReadBy_FirstReader_PopulatesReadByAndScalarReadAt()
    {
        var message = NewMessage();
        var readAt = Created.AddSeconds(10);

        var changed = message.MarkReadBy(Reader1, readAt);

        changed.Should().BeTrue();
        message.ReadBy.Should().ContainSingle(r => r.UserId == Reader1 && r.ReadAtUtc == readAt);
        message.ReadAtUtc.Should().Be(readAt);
        message.State.Should().Be(MessageState.Read);
        message.DeliveredAtUtc.Should().Be(readAt);
    }

    [Fact]
    public void MarkReadBy_SecondReader_AddsToReadBy_AndKeepsScalarReadAtFromFirst()
    {
        var message = NewMessage();
        message.MarkReadBy(Reader1, Created.AddSeconds(10));

        var changed = message.MarkReadBy(Reader2, Created.AddSeconds(20));

        changed.Should().BeTrue();
        message.ReadBy.Should().HaveCount(2);
        message.ReadAtUtc.Should().Be(Created.AddSeconds(10));
    }

    [Fact]
    public void MarkReadBy_SameUserTwice_IsIdempotent()
    {
        var message = NewMessage();
        message.MarkReadBy(Reader1, Created.AddSeconds(10));

        var changed = message.MarkReadBy(Reader1, Created.AddSeconds(20));

        changed.Should().BeFalse();
        message.ReadBy.Should().HaveCount(1);
    }

    [Fact]
    public void MarkReadBy_OnDeletedMessage_ReturnsFalse()
    {
        var message = NewMessage();
        message.MarkDeletedForEveryone(Sender, Created.AddMinutes(1), Ttl);

        var changed = message.MarkReadBy(Reader1, Created.AddMinutes(2));

        changed.Should().BeFalse();
        message.ReadBy.Should().BeEmpty();
    }
}
