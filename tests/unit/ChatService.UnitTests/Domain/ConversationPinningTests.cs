using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class ConversationPinningTests
{
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);

    private static Conversation NewDirect() => Conversation.OpenDirect(UserA, UserB, Now);

    [Fact]
    public void NewConversation_HasNoPinnedMessages()
    {
        var conversation = NewDirect();

        conversation.PinnedMessageIds.Should().BeEmpty();
    }

    [Fact]
    public void PinMessage_FirstTime_AddsToPinnedList()
    {
        var conversation = NewDirect();
        var messageId = Guid.NewGuid();

        var pinned = conversation.PinMessage(messageId, maxPinned: 5);

        pinned.Should().BeTrue();
        conversation.PinnedMessageIds.Should().ContainSingle(id => id == messageId);
        conversation.IsPinned(messageId).Should().BeTrue();
    }

    [Fact]
    public void PinMessage_AlreadyPinned_IsNoopReturnsFalse()
    {
        var conversation = NewDirect();
        var messageId = Guid.NewGuid();
        conversation.PinMessage(messageId, maxPinned: 5);

        var second = conversation.PinMessage(messageId, maxPinned: 5);

        second.Should().BeFalse();
        conversation.PinnedMessageIds.Should().HaveCount(1);
    }

    [Fact]
    public void PinMessage_AtCap_ReturnsFalse()
    {
        var conversation = NewDirect();
        for (var i = 0; i < 5; i++)
        {
            conversation.PinMessage(Guid.NewGuid(), maxPinned: 5).Should().BeTrue();
        }

        var sixth = conversation.PinMessage(Guid.NewGuid(), maxPinned: 5);

        sixth.Should().BeFalse();
        conversation.PinnedMessageIds.Should().HaveCount(5);
    }

    [Fact]
    public void PinMessage_NonPositiveMaxPinned_Throws()
    {
        var conversation = NewDirect();

        var act = () => conversation.PinMessage(Guid.NewGuid(), maxPinned: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnpinMessage_PinnedMessage_RemovesAndReturnsTrue()
    {
        var conversation = NewDirect();
        var messageId = Guid.NewGuid();
        conversation.PinMessage(messageId, maxPinned: 5);

        var unpinned = conversation.UnpinMessage(messageId);

        unpinned.Should().BeTrue();
        conversation.PinnedMessageIds.Should().BeEmpty();
        conversation.IsPinned(messageId).Should().BeFalse();
    }

    [Fact]
    public void UnpinMessage_NotPinned_ReturnsFalse()
    {
        var conversation = NewDirect();

        var unpinned = conversation.UnpinMessage(Guid.NewGuid());

        unpinned.Should().BeFalse();
    }

    [Fact]
    public void Hydrate_RestoresPinnedMessageIds()
    {
        var pinned = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var conversation = Conversation.Hydrate(
            id: "abc",
            type: ConversationType.Direct,
            participants: new[] { UserA, UserB },
            createdAtUtc: Now,
            lastMessageAtUtc: Now,
            lastMessagePreview: null,
            pinnedMessageIds: pinned);

        conversation.PinnedMessageIds.Should().BeEquivalentTo(pinned);
    }
}
