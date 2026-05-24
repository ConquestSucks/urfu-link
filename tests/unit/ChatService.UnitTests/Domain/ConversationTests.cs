using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class ConversationTests
{
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void OpenDirect_SamePair_ProducesSameId_RegardlessOfOrder()
    {
        var first = Conversation.OpenDirect(UserA, UserB, Now);
        var second = Conversation.OpenDirect(UserB, UserA, Now);

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public void OpenDirect_DifferentPair_ProducesDifferentId()
    {
        var third = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var first = Conversation.OpenDirect(UserA, UserB, Now);
        var other = Conversation.OpenDirect(UserA, third, Now);

        other.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public void OpenDirect_SetsTypeAndParticipantsAndTimestamps()
    {
        var conversation = Conversation.OpenDirect(UserA, UserB, Now);

        conversation.Type.Should().Be(ConversationType.Direct);
        conversation.Participants.Should().BeEquivalentTo(new[] { UserA, UserB });
        conversation.CreatedAtUtc.Should().Be(Now);
        conversation.LastMessageAtUtc.Should().Be(Now);
        conversation.LastMessagePreview.Should().BeNull();
    }

    [Fact]
    public void OpenDirect_RejectsSelfChat()
    {
        var act = () => Conversation.OpenDirect(UserA, UserA, Now);

        act.Should().Throw<ArgumentException>().WithMessage("*self*");
    }

    [Fact]
    public void IsParticipant_ReturnsTrueForBothMembers_FalseForOthers()
    {
        var conversation = Conversation.OpenDirect(UserA, UserB, Now);
        var other = Guid.Parse("99999999-9999-9999-9999-999999999999");

        conversation.IsParticipant(UserA).Should().BeTrue();
        conversation.IsParticipant(UserB).Should().BeTrue();
        conversation.IsParticipant(other).Should().BeFalse();
    }

    [Fact]
    public void RegisterMessage_UpdatesLastMessagePreviewAndTimestamp()
    {
        var conversation = Conversation.OpenDirect(UserA, UserB, Now);
        var sentAt = Now.AddMinutes(5);
        var preview = new MessagePreview(UserA, "hello", sentAt, hasAttachments: false);

        conversation.RegisterMessage(preview, sentAt);

        conversation.LastMessagePreview.Should().Be(preview);
        conversation.LastMessageAtUtc.Should().Be(sentAt);
    }
}
