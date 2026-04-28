using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class MessageDeleteTests
{
    private const string ConversationId = "abc";
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Recipient = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Created = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(48);

    private static Message NewMessage() => Message.Send(
        id: Guid.NewGuid(),
        conversationId: ConversationId,
        senderId: Sender,
        body: "hello",
        attachments: new[] { new Attachment(Guid.NewGuid(), AttachmentType.Image, null, "f.png", 10, "image/png") },
        clientMessageId: "client-1",
        createdAtUtc: Created);

    [Fact]
    public void MarkDeletedForEveryone_WithinTtl_ClearsContent_AndSetsTombstone()
    {
        var message = NewMessage();
        message.AddReaction(Recipient, "👍", Created.AddSeconds(1));
        var deletedAt = Created.AddMinutes(1);

        var deleted = message.MarkDeletedForEveryone(Sender, deletedAt, Ttl);

        deleted.Should().BeTrue();
        message.State.Should().Be(MessageState.Deleted);
        message.DeletedAtUtc.Should().Be(deletedAt);
        message.DeletedBy.Should().Be(Sender);
        message.DeleteMode.Should().Be(DeleteMode.ForEveryone);
        message.Body.Should().BeEmpty();
        message.Attachments.Should().BeEmpty();
        message.Reactions.Should().BeEmpty();
    }

    [Fact]
    public void MarkDeletedForEveryone_PastTtl_ReturnsFalse_AndKeepsContent()
    {
        var message = NewMessage();
        var deletedAt = Created.AddHours(49);

        var deleted = message.MarkDeletedForEveryone(Sender, deletedAt, Ttl);

        deleted.Should().BeFalse();
        message.State.Should().Be(MessageState.Sent);
        message.Body.Should().Be("hello");
    }

    [Fact]
    public void MarkDeletedForEveryone_AlreadyDeleted_IsIdempotentNoop()
    {
        var message = NewMessage();
        message.MarkDeletedForEveryone(Sender, Created.AddMinutes(1), Ttl);

        var second = message.MarkDeletedForEveryone(Sender, Created.AddMinutes(2), Ttl);

        second.Should().BeFalse();
        message.DeletedAtUtc.Should().Be(Created.AddMinutes(1));
    }

    [Fact]
    public void MarkDeletedForMe_AddsUserToHiddenFor_Idempotent()
    {
        var message = NewMessage();

        message.MarkDeletedForMe(Recipient).Should().BeTrue();
        message.HiddenFor.Should().BeEquivalentTo(new[] { Recipient });

        message.MarkDeletedForMe(Recipient).Should().BeFalse();
        message.HiddenFor.Should().HaveCount(1);
    }

    [Fact]
    public void MarkDeletedForMe_DoesNotAffectVisibleStateForOthers()
    {
        var message = NewMessage();

        message.MarkDeletedForMe(Recipient);

        message.State.Should().Be(MessageState.Sent);
        message.Body.Should().Be("hello");
        message.Attachments.Should().HaveCount(1);
        message.IsHiddenFor(Recipient).Should().BeTrue();
        message.IsHiddenFor(Sender).Should().BeFalse();
    }

    [Fact]
    public void IsDeletableForEveryoneBy_NonAuthor_ReturnsFalse()
    {
        var message = NewMessage();

        message.IsDeletableForEveryoneBy(Recipient, Created.AddMinutes(1), Ttl).Should().BeFalse();
    }

    [Fact]
    public void IsDeletableForEveryoneBy_AuthorWithinTtl_ReturnsTrue()
    {
        var message = NewMessage();

        message.IsDeletableForEveryoneBy(Sender, Created.AddMinutes(1), Ttl).Should().BeTrue();
    }
}
