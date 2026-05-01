using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Realtime;

// Test fakes intentionally expose mutable List<T> so tests can assert against captured records
// with Should().ContainSingle / Where / etc. The CA1002 collection-type rule is irrelevant here.
#pragma warning disable CA1002

namespace ChatService.IntegrationTests.Infrastructure;

public sealed record FakeBroadcastConversationRecord(IReadOnlyList<Guid> Recipients, ConversationDto Conversation);
public sealed record FakeBroadcastParticipantJoinedRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid UserId, ParticipantRole Role);
public sealed record FakeBroadcastParticipantLeftRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid UserId);
public sealed record FakeBroadcastParticipantRoleChangedRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid UserId, ParticipantRole NewRole);
public sealed record FakeBroadcastConversationArchivedRecord(IReadOnlyList<Guid> Recipients, string ConversationId, DateTimeOffset ArchivedAtUtc);
public sealed record FakeBroadcastMessageReceivedRecord(IReadOnlyList<Guid> Recipients, MessageDto Message);
public sealed record FakeBroadcastMessageDeletedRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid MessageId, DeleteMode Mode, Guid DeletedBy);
public sealed record FakeBroadcastMessageEditedRecord(IReadOnlyList<Guid> Recipients, MessageDto Message);
public sealed record FakeBroadcastPinsUpdatedRecord(IReadOnlyList<Guid> Recipients, string ConversationId, IReadOnlyList<MessageDto> PinnedMessages);
public sealed record FakeBroadcastReactionUpdatedRecord(IReadOnlyList<Guid> Recipients, Guid MessageId, IReadOnlyDictionary<string, IReadOnlyList<Guid>> Summary);
public sealed record FakeBroadcastMessageDeliveredRecord(IReadOnlyList<Guid> Recipients, string ConversationId, IReadOnlyList<Guid> MessageIds, Guid RecipientUserId);
public sealed record FakeBroadcastMessageReadRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid UpToMessageId, Guid ReaderUserId);
public sealed record FakeBroadcastMessageReadByRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid MessageId, Guid ReaderUserId, DateTimeOffset ReadAtUtc);
public sealed record FakeBroadcastThreadReplyRecord(IReadOnlyList<Guid> Recipients, Guid RootMessageId, MessageDto Reply);
public sealed record FakeBroadcastThreadRootUpdatedRecord(IReadOnlyList<Guid> Recipients, string ConversationId, Guid RootMessageId, int ReplyCount, IReadOnlyList<Guid> Participants, DateTimeOffset LastReplyAtUtc);
public sealed record FakeBroadcastThreadParticipantJoinedRecord(IReadOnlyList<Guid> Recipients, Guid RootMessageId, Guid JoinedUserId, ThreadSubscriptionReason Reason);

/// <summary>
/// Captures every notification the production <see cref="ChatBroadcaster"/> would have emitted,
/// so tests can assert which clients would have seen which events without booting SignalR.
/// </summary>
public sealed class FakeChatBroadcaster : IChatBroadcaster
{
    public List<FakeBroadcastConversationRecord> ConversationUpdated { get; } = new();
    public List<FakeBroadcastConversationRecord> ConversationCreated { get; } = new();
    public List<FakeBroadcastParticipantJoinedRecord> ParticipantJoined { get; } = new();
    public List<FakeBroadcastParticipantLeftRecord> ParticipantLeft { get; } = new();
    public List<FakeBroadcastParticipantRoleChangedRecord> ParticipantRoleChanged { get; } = new();
    public List<FakeBroadcastConversationArchivedRecord> ConversationArchived { get; } = new();
    public List<FakeBroadcastMessageReceivedRecord> MessageReceived { get; } = new();
    public List<FakeBroadcastMessageDeletedRecord> MessageDeleted { get; } = new();
    public List<FakeBroadcastMessageEditedRecord> MessageEdited { get; } = new();
    public List<FakeBroadcastPinsUpdatedRecord> PinsUpdated { get; } = new();
    public List<FakeBroadcastReactionUpdatedRecord> ReactionUpdated { get; } = new();
    public List<FakeBroadcastMessageDeliveredRecord> MessageDelivered { get; } = new();
    public List<FakeBroadcastMessageReadRecord> MessageRead { get; } = new();
    public List<FakeBroadcastMessageReadByRecord> MessageReadBy { get; } = new();
    public List<FakeBroadcastThreadReplyRecord> ThreadReplyReceived { get; } = new();
    public List<FakeBroadcastThreadRootUpdatedRecord> ThreadRootUpdated { get; } = new();
    public List<FakeBroadcastThreadParticipantJoinedRecord> ThreadParticipantJoined { get; } = new();

    public void Reset()
    {
        ConversationUpdated.Clear();
        ConversationCreated.Clear();
        ParticipantJoined.Clear();
        ParticipantLeft.Clear();
        ParticipantRoleChanged.Clear();
        ConversationArchived.Clear();
        MessageReceived.Clear();
        MessageDeleted.Clear();
        MessageEdited.Clear();
        PinsUpdated.Clear();
        ReactionUpdated.Clear();
        MessageDelivered.Clear();
        MessageRead.Clear();
        MessageReadBy.Clear();
        ThreadReplyReceived.Clear();
        ThreadRootUpdated.Clear();
        ThreadParticipantJoined.Clear();
    }

    public Task NotifyConversationUpdatedAsync(IReadOnlyList<Guid> participantUserIds, ConversationDto conversation, CancellationToken cancellationToken)
    {
        ConversationUpdated.Add(new(participantUserIds.ToList(), conversation));
        return Task.CompletedTask;
    }

    public Task NotifyConversationCreatedAsync(IReadOnlyList<Guid> recipientUserIds, ConversationDto conversation, CancellationToken cancellationToken)
    {
        ConversationCreated.Add(new(recipientUserIds.ToList(), conversation));
        return Task.CompletedTask;
    }

    public Task NotifyParticipantJoinedAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, Guid userId, ParticipantRole role, CancellationToken cancellationToken)
    {
        ParticipantJoined.Add(new(recipientUserIds.ToList(), conversationId, userId, role));
        return Task.CompletedTask;
    }

    public Task NotifyParticipantLeftAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, Guid userId, CancellationToken cancellationToken)
    {
        ParticipantLeft.Add(new(recipientUserIds.ToList(), conversationId, userId));
        return Task.CompletedTask;
    }

    public Task NotifyParticipantRoleChangedAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, Guid userId, ParticipantRole newRole, CancellationToken cancellationToken)
    {
        ParticipantRoleChanged.Add(new(recipientUserIds.ToList(), conversationId, userId, newRole));
        return Task.CompletedTask;
    }

    public Task NotifyConversationArchivedAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, DateTimeOffset archivedAtUtc, CancellationToken cancellationToken)
    {
        ConversationArchived.Add(new(recipientUserIds.ToList(), conversationId, archivedAtUtc));
        return Task.CompletedTask;
    }

    public Task NotifyMessageReceivedAsync(IReadOnlyList<Guid> recipientUserIds, MessageDto message, CancellationToken cancellationToken)
    {
        MessageReceived.Add(new(recipientUserIds.ToList(), message));
        return Task.CompletedTask;
    }

    public Task NotifyMessageDeliveredAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, IReadOnlyList<Guid> messageIds, Guid recipientUserId, CancellationToken cancellationToken)
    {
        MessageDelivered.Add(new(recipientUserIds.ToList(), conversationId, messageIds.ToList(), recipientUserId));
        return Task.CompletedTask;
    }

    public Task NotifyMessageReadAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, Guid upToMessageId, Guid readerUserId, CancellationToken cancellationToken)
    {
        MessageRead.Add(new(recipientUserIds.ToList(), conversationId, upToMessageId, readerUserId));
        return Task.CompletedTask;
    }

    public Task NotifyMessageReadByAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, Guid messageId, Guid readerUserId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        MessageReadBy.Add(new(recipientUserIds.ToList(), conversationId, messageId, readerUserId, readAtUtc));
        return Task.CompletedTask;
    }

    public Task NotifyMessageEditedAsync(IReadOnlyList<Guid> recipientUserIds, MessageDto message, CancellationToken cancellationToken)
    {
        MessageEdited.Add(new(recipientUserIds.ToList(), message));
        return Task.CompletedTask;
    }

    public Task NotifyMessageDeletedAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, Guid messageId, DeleteMode mode, Guid deletedBy, CancellationToken cancellationToken)
    {
        MessageDeleted.Add(new(recipientUserIds.ToList(), conversationId, messageId, mode, deletedBy));
        return Task.CompletedTask;
    }

    public Task NotifyReactionUpdatedAsync(IReadOnlyList<Guid> recipientUserIds, Guid messageId, IReadOnlyDictionary<string, IReadOnlyList<Guid>> summary, CancellationToken cancellationToken)
    {
        ReactionUpdated.Add(new(recipientUserIds.ToList(), messageId, summary));
        return Task.CompletedTask;
    }

    public Task NotifyPinsUpdatedAsync(IReadOnlyList<Guid> recipientUserIds, string conversationId, IReadOnlyList<MessageDto> pinnedMessages, CancellationToken cancellationToken)
    {
        PinsUpdated.Add(new(recipientUserIds.ToList(), conversationId, pinnedMessages.ToList()));
        return Task.CompletedTask;
    }

    public Task NotifyThreadReplyReceivedAsync(IReadOnlyList<Guid> subscriberUserIds, Guid rootMessageId, MessageDto reply, CancellationToken cancellationToken)
    {
        ThreadReplyReceived.Add(new(subscriberUserIds.ToList(), rootMessageId, reply));
        return Task.CompletedTask;
    }

    public Task NotifyThreadRootUpdatedAsync(IReadOnlyList<Guid> participantUserIds, string conversationId, Guid rootMessageId, int replyCount, IReadOnlyList<Guid> participants, DateTimeOffset lastReplyAtUtc, CancellationToken cancellationToken)
    {
        ThreadRootUpdated.Add(new(participantUserIds.ToList(), conversationId, rootMessageId, replyCount, participants.ToList(), lastReplyAtUtc));
        return Task.CompletedTask;
    }

    public Task NotifyThreadParticipantJoinedAsync(IReadOnlyList<Guid> subscriberUserIds, Guid rootMessageId, Guid joinedUserId, ThreadSubscriptionReason reason, CancellationToken cancellationToken)
    {
        ThreadParticipantJoined.Add(new(subscriberUserIds.ToList(), rootMessageId, joinedUserId, reason));
        return Task.CompletedTask;
    }
}
