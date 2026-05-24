using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class MessageReactionsTests
{
    private const string ConversationId = "abc";
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserA = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserB = Guid.Parse("33333333-3333-3333-3333-333333333333");
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
    public void AddReaction_NewReaction_IsAddedAndReturnsTrue()
    {
        var message = NewMessage();

        var changed = message.AddReaction(UserA, "👍", Created.AddSeconds(1));

        changed.Should().BeTrue();
        message.Reactions.Should().ContainSingle(r => r.UserId == UserA && r.Emoji == "👍");
    }

    [Fact]
    public void AddReaction_SameUserSameEmoji_IsNoop()
    {
        var message = NewMessage();
        message.AddReaction(UserA, "👍", Created.AddSeconds(1));

        var changed = message.AddReaction(UserA, "👍", Created.AddSeconds(2));

        changed.Should().BeFalse();
        message.Reactions.Should().HaveCount(1);
    }

    [Fact]
    public void AddReaction_SameUserDifferentEmoji_ReplacesPriorReaction()
    {
        var message = NewMessage();
        message.AddReaction(UserA, "👍", Created.AddSeconds(1));

        var changed = message.AddReaction(UserA, "❤", Created.AddSeconds(2));

        changed.Should().BeTrue();
        message.Reactions.Should().ContainSingle();
        message.Reactions[0].Emoji.Should().Be("❤");
        message.Reactions[0].CreatedAtUtc.Should().Be(Created.AddSeconds(2));
    }

    [Fact]
    public void AddReaction_DifferentUsers_AccumulateIndependently()
    {
        var message = NewMessage();
        message.AddReaction(UserA, "👍", Created.AddSeconds(1));
        message.AddReaction(UserB, "👍", Created.AddSeconds(2));
        message.AddReaction(UserB, "❤", Created.AddSeconds(3));

        message.Reactions.Should().HaveCount(2);
        message.Reactions.Should().Contain(r => r.UserId == UserA && r.Emoji == "👍");
        message.Reactions.Should().Contain(r => r.UserId == UserB && r.Emoji == "❤");
    }

    [Fact]
    public void AddReaction_ToDeletedMessage_ReturnsFalse()
    {
        var message = NewMessage();
        message.MarkDeletedForEveryone(Sender, Created.AddMinutes(1), Ttl);

        var changed = message.AddReaction(UserA, "👍", Created.AddMinutes(2));

        changed.Should().BeFalse();
        message.Reactions.Should().BeEmpty();
    }

    [Fact]
    public void RemoveReaction_ExistingReaction_ReturnsTrueAndRemoves()
    {
        var message = NewMessage();
        message.AddReaction(UserA, "👍", Created.AddSeconds(1));

        var removed = message.RemoveReaction(UserA, "👍");

        removed.Should().BeTrue();
        message.Reactions.Should().BeEmpty();
    }

    [Fact]
    public void RemoveReaction_DifferentEmoji_ReturnsFalse_AndKeepsExisting()
    {
        var message = NewMessage();
        message.AddReaction(UserA, "👍", Created.AddSeconds(1));

        var removed = message.RemoveReaction(UserA, "❤");

        removed.Should().BeFalse();
        message.Reactions.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveReaction_FromUserWithoutReaction_ReturnsFalse()
    {
        var message = NewMessage();

        var removed = message.RemoveReaction(UserA, "👍");

        removed.Should().BeFalse();
    }

    [Fact]
    public void AddReaction_WithEmptyEmoji_ThrowsArgumentException()
    {
        var message = NewMessage();

        var act = () => message.AddReaction(UserA, "", Created.AddSeconds(1));

        act.Should().Throw<ArgumentException>();
    }
}
