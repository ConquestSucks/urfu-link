using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class MessageEditTests
{
    private const string ConversationId = "abc";
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Outsider = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset Created = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(48);

    private static Message NewMessage() => Message.Send(
        id: Guid.NewGuid(),
        conversationId: ConversationId,
        senderId: Sender,
        body: "hello",
        attachments: Array.Empty<Attachment>(),
        clientMessageId: "client-1",
        createdAtUtc: Created);

    [Fact]
    public void IsAuthor_ReturnsTrueForSender_FalseForOthers()
    {
        var message = NewMessage();

        message.IsAuthor(Sender).Should().BeTrue();
        message.IsAuthor(Outsider).Should().BeFalse();
    }

    [Fact]
    public void IsEditableBy_AuthorWithinTtl_ReturnsTrue()
    {
        var message = NewMessage();

        message.IsEditableBy(Sender, Created.AddHours(1), Ttl).Should().BeTrue();
    }

    [Fact]
    public void IsEditableBy_NonAuthor_ReturnsFalse()
    {
        var message = NewMessage();

        message.IsEditableBy(Outsider, Created.AddHours(1), Ttl).Should().BeFalse();
    }

    [Fact]
    public void IsEditableBy_PastTtl_ReturnsFalse()
    {
        var message = NewMessage();

        message.IsEditableBy(Sender, Created.AddHours(49), Ttl).Should().BeFalse();
    }

    [Fact]
    public void IsEditableBy_DeletedMessage_ReturnsFalse()
    {
        var message = NewMessage();
        message.MarkDeletedForEveryone(Sender, Created.AddMinutes(1), Ttl);

        message.IsEditableBy(Sender, Created.AddMinutes(2), Ttl).Should().BeFalse();
    }

    [Fact]
    public void Edit_WithinTtl_UpdatesBody_AndRecordsHistory_AndSetsEditedAtUtc()
    {
        var message = NewMessage();
        var editedAt = Created.AddMinutes(10);

        var changed = message.Edit("updated", Array.Empty<Guid>(), editedAt, Ttl);

        changed.Should().BeTrue();
        message.Body.Should().Be("updated");
        message.EditedAtUtc.Should().Be(editedAt);
        message.EditHistory.Should().HaveCount(1);
        message.EditHistory[0].Body.Should().Be("hello");
        message.EditHistory[0].EditedAtUtc.Should().Be(Created);
    }

    [Fact]
    public void Edit_PastTtl_ReturnsFalse_AndKeepsBody()
    {
        var message = NewMessage();

        var changed = message.Edit("late", Array.Empty<Guid>(), Created.AddHours(49), Ttl);

        changed.Should().BeFalse();
        message.Body.Should().Be("hello");
        message.EditedAtUtc.Should().BeNull();
        message.EditHistory.Should().BeEmpty();
    }

    [Fact]
    public void Edit_DeletedMessage_ReturnsFalse()
    {
        var message = NewMessage();
        message.MarkDeletedForEveryone(Sender, Created.AddMinutes(1), Ttl);

        var changed = message.Edit("after-delete", Array.Empty<Guid>(), Created.AddMinutes(2), Ttl);

        changed.Should().BeFalse();
    }

    [Fact]
    public void Edit_SameBodyAndMentions_IsNoop()
    {
        var message = NewMessage();

        var changed = message.Edit("hello", Array.Empty<Guid>(), Created.AddMinutes(1), Ttl);

        changed.Should().BeFalse();
        message.EditHistory.Should().BeEmpty();
        message.EditedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Edit_ReplacesMentionsList()
    {
        var message = NewMessage();
        var mentioned = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var changed = message.Edit("hi @user", mentioned, Created.AddMinutes(1), Ttl);

        changed.Should().BeTrue();
        message.Mentions.Should().BeEquivalentTo(mentioned);
    }

    [Fact]
    public void Edit_TwiceAppendsTwoEntriesToHistory_KeepingChronologicalOrder()
    {
        var message = NewMessage();
        message.Edit("first", Array.Empty<Guid>(), Created.AddMinutes(1), Ttl);
        message.Edit("second", Array.Empty<Guid>(), Created.AddMinutes(2), Ttl);

        message.EditHistory.Should().HaveCount(2);
        message.EditHistory[0].Body.Should().Be("hello");
        message.EditHistory[0].EditedAtUtc.Should().Be(Created);
        message.EditHistory[1].Body.Should().Be("first");
        message.EditHistory[1].EditedAtUtc.Should().Be(Created.AddMinutes(1));
        message.Body.Should().Be("second");
    }
}
