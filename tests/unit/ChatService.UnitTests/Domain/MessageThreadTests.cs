using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class MessageThreadTests
{
    private const string ConversationId = "abc";
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Replier = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Created = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);

    private static Message NewRootMessage() => Message.Send(
        id: Guid.NewGuid(),
        conversationId: ConversationId,
        senderId: Sender,
        body: "root",
        attachments: Array.Empty<Attachment>(),
        clientMessageId: "client-root",
        createdAtUtc: Created);

    private static Message NewReply(Guid rootId, Guid? replyId = null) => Message.SendAsThreadReply(
        id: replyId ?? Guid.NewGuid(),
        conversationId: ConversationId,
        senderId: Replier,
        body: "reply",
        attachments: Array.Empty<Attachment>(),
        clientMessageId: "client-reply",
        createdAtUtc: Created.AddSeconds(10),
        threadRootId: rootId);

    [Fact]
    public void Send_DefaultMessage_HasNoThreadFields()
    {
        var message = NewRootMessage();

        message.ThreadRootId.Should().BeNull();
        message.ThreadReplyCount.Should().Be(0);
        message.ThreadParticipants.Should().BeEmpty();
        message.ThreadLastReplyAtUtc.Should().BeNull();
        message.IsThreadReply.Should().BeFalse();
    }

    [Fact]
    public void SendAsThreadReply_CreatesReply_WithThreadRootIdSet()
    {
        var root = NewRootMessage();

        var reply = NewReply(root.Id);

        reply.ThreadRootId.Should().Be(root.Id);
        reply.IsThreadReply.Should().BeTrue();
    }

    [Fact]
    public void SendAsThreadReply_RejectsEmptyRootMessageId()
    {
        var act = () => Message.SendAsThreadReply(
            id: Guid.NewGuid(),
            conversationId: ConversationId,
            senderId: Replier,
            body: "reply",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "client-reply",
            createdAtUtc: Created.AddSeconds(10),
            threadRootId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*threadRootId*");
    }

    [Fact]
    public void SendAsThreadReply_RejectsSelfReference()
    {
        var sameId = Guid.NewGuid();

        var act = () => Message.SendAsThreadReply(
            id: sameId,
            conversationId: ConversationId,
            senderId: Replier,
            body: "reply",
            attachments: Array.Empty<Attachment>(),
            clientMessageId: "client-reply",
            createdAtUtc: Created.AddSeconds(10),
            threadRootId: sameId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*self*");
    }

    [Fact]
    public void SendAsThreadReply_NewReply_HasNoOwnThreadDenorms()
    {
        var root = NewRootMessage();

        var reply = NewReply(root.Id);

        reply.ThreadReplyCount.Should().Be(0);
        reply.ThreadParticipants.Should().BeEmpty();
        reply.ThreadLastReplyAtUtc.Should().BeNull();
    }

    [Fact]
    public void SendAsThreadReply_StartsInSentState()
    {
        var root = NewRootMessage();

        var reply = NewReply(root.Id);

        reply.State.Should().Be(MessageState.Sent);
        reply.DeliveredAtUtc.Should().BeNull();
        reply.ReadAtUtc.Should().BeNull();
    }

    [Fact]
    public void IncrementThreadDenorm_OnRoot_IncrementsReplyCount()
    {
        var root = NewRootMessage();
        var replyAt = Created.AddSeconds(10);

        root.IncrementThreadDenorm(Replier, replyAt);

        root.ThreadReplyCount.Should().Be(1);
    }

    [Fact]
    public void IncrementThreadDenorm_OnRoot_AddsParticipantOnFirstReplyFromUser()
    {
        var root = NewRootMessage();
        var replyAt = Created.AddSeconds(10);

        root.IncrementThreadDenorm(Replier, replyAt);

        root.ThreadParticipants.Should().ContainSingle().Which.Should().Be(Replier);
    }

    [Fact]
    public void IncrementThreadDenorm_OnRoot_DoesNotDuplicateParticipantOnRepeatReply()
    {
        var root = NewRootMessage();

        root.IncrementThreadDenorm(Replier, Created.AddSeconds(10));
        root.IncrementThreadDenorm(Replier, Created.AddSeconds(20));

        root.ThreadParticipants.Should().ContainSingle().Which.Should().Be(Replier);
        root.ThreadReplyCount.Should().Be(2);
    }

    [Fact]
    public void IncrementThreadDenorm_OnRoot_AccumulatesDistinctParticipants()
    {
        var root = NewRootMessage();
        var otherReplier = Guid.Parse("33333333-3333-3333-3333-333333333333");

        root.IncrementThreadDenorm(Replier, Created.AddSeconds(10));
        root.IncrementThreadDenorm(otherReplier, Created.AddSeconds(20));

        root.ThreadParticipants.Should().BeEquivalentTo(new[] { Replier, otherReplier });
    }

    [Fact]
    public void IncrementThreadDenorm_OnRoot_UpdatesLastReplyAt()
    {
        var root = NewRootMessage();
        var firstReply = Created.AddSeconds(10);
        var secondReply = Created.AddSeconds(20);

        root.IncrementThreadDenorm(Replier, firstReply);
        root.IncrementThreadDenorm(Replier, secondReply);

        root.ThreadLastReplyAtUtc.Should().Be(secondReply);
    }

    [Fact]
    public void IncrementThreadDenorm_OnReply_Throws()
    {
        var root = NewRootMessage();
        var reply = NewReply(root.Id);

        var act = () => reply.IncrementThreadDenorm(Replier, Created.AddSeconds(10));

        act.Should().Throw<InvalidOperationException>().WithMessage("*root*");
    }
}
