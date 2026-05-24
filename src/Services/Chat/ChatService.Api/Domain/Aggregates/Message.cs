using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Domain.Aggregates;

public sealed class Message
{
    private readonly List<Attachment> _attachments;
    private readonly List<EditHistoryEntry> _editHistory;
    private readonly List<Guid> _hiddenFor;
    private readonly List<Reaction> _reactions;
    private readonly List<Guid> _mentions;
    private readonly List<ReadReceipt> _readBy;
    private readonly List<Guid> _threadParticipants;

    private Message(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        MessageState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? deliveredAtUtc,
        DateTimeOffset? readAtUtc,
        DateTimeOffset? editedAtUtc,
        IEnumerable<EditHistoryEntry>? editHistory,
        DateTimeOffset? deletedAtUtc,
        Guid? deletedBy,
        DeleteMode? deleteMode,
        IEnumerable<Guid>? hiddenFor,
        IEnumerable<Reaction>? reactions,
        IEnumerable<Guid>? mentions,
        ReplyTo? replyTo,
        ForwardedFrom? forwardedFrom,
        IEnumerable<ReadReceipt>? readBy,
        Guid? threadRootId,
        int threadReplyCount,
        IEnumerable<Guid>? threadParticipants,
        DateTimeOffset? threadLastReplyAtUtc,
        ParticipantRole authorRole)
    {
        Id = id;
        ConversationId = conversationId;
        SenderId = senderId;
        Body = body;
        _attachments = attachments.ToList();
        ClientMessageId = clientMessageId;
        State = state;
        CreatedAtUtc = createdAtUtc;
        DeliveredAtUtc = deliveredAtUtc;
        ReadAtUtc = readAtUtc;
        EditedAtUtc = editedAtUtc;
        _editHistory = editHistory?.ToList() ?? [];
        DeletedAtUtc = deletedAtUtc;
        DeletedBy = deletedBy;
        DeleteMode = deleteMode;
        _hiddenFor = hiddenFor?.ToList() ?? [];
        _reactions = reactions?.ToList() ?? [];
        _mentions = mentions?.ToList() ?? [];
        ReplyTo = replyTo;
        ForwardedFrom = forwardedFrom;
        _readBy = readBy?.ToList() ?? [];
        ThreadRootId = threadRootId;
        ThreadReplyCount = threadReplyCount;
        _threadParticipants = threadParticipants?.ToList() ?? [];
        ThreadLastReplyAtUtc = threadLastReplyAtUtc;
        AuthorRole = authorRole;
    }

    public Guid Id { get; }

    public string ConversationId { get; }

    public Guid SenderId { get; }

    public string Body { get; private set; }

    public IReadOnlyList<Attachment> Attachments => _attachments;

    public string ClientMessageId { get; }

    public MessageState State { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public DateTimeOffset? EditedAtUtc { get; private set; }

    public IReadOnlyList<EditHistoryEntry> EditHistory => _editHistory;

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public DeleteMode? DeleteMode { get; private set; }

    public IReadOnlyList<Guid> HiddenFor => _hiddenFor;

    public IReadOnlyList<Reaction> Reactions => _reactions;

    public IReadOnlyList<Guid> Mentions => _mentions;

    public ReplyTo? ReplyTo { get; private set; }

    public ForwardedFrom? ForwardedFrom { get; private set; }

    public IReadOnlyList<ReadReceipt> ReadBy => _readBy;

    public bool HasAttachments => _attachments.Count > 0;

    /// <summary>
    /// When set, this message is a reply within the thread rooted at <see cref="ThreadRootId"/>.
    /// On root and main-flow messages this is <c>null</c>.
    /// </summary>
    public Guid? ThreadRootId { get; }

    /// <summary>
    /// Denormalized count of replies in the thread rooted at this message. Always 0 on
    /// thread replies (denorms live only on the root).
    /// </summary>
    public int ThreadReplyCount { get; private set; }

    /// <summary>
    /// Denormalized unique senders that have replied in this thread. Empty on thread replies.
    /// </summary>
    public IReadOnlyList<Guid> ThreadParticipants => _threadParticipants;

    /// <summary>
    /// Denormalized timestamp of the last reply in this thread. <c>null</c> if no replies yet,
    /// or on thread replies themselves.
    /// </summary>
    public DateTimeOffset? ThreadLastReplyAtUtc { get; private set; }

    public bool IsThreadReply => ThreadRootId.HasValue;

    /// <summary>
    /// Denormalised role of the author at the moment the message was persisted. For direct
    /// chats this is <see cref="ParticipantRole.Member"/>; for discipline groups it captures
    /// whether the sender was a Teacher or Student. Stored on the message so the UI can render
    /// the role badge without a follow-up lookup against the conversation.
    /// </summary>
    public ParticipantRole AuthorRole { get; }

    public static Message Send(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        DateTimeOffset createdAtUtc,
        IEnumerable<Guid>? mentions = null,
        ReplyTo? replyTo = null,
        ForwardedFrom? forwardedFrom = null,
        ParticipantRole authorRole = ParticipantRole.Member)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientMessageId);

        return new Message(
            id,
            conversationId,
            senderId,
            body ?? string.Empty,
            attachments,
            clientMessageId,
            MessageState.Sent,
            createdAtUtc,
            deliveredAtUtc: null,
            readAtUtc: null,
            editedAtUtc: null,
            editHistory: null,
            deletedAtUtc: null,
            deletedBy: null,
            deleteMode: null,
            hiddenFor: null,
            reactions: null,
            mentions: mentions,
            replyTo: replyTo,
            forwardedFrom: forwardedFrom,
            readBy: null,
            threadRootId: null,
            threadReplyCount: 0,
            threadParticipants: null,
            threadLastReplyAtUtc: null,
            authorRole: authorRole);
    }

    /// <summary>
    /// Creates a new reply in the thread rooted at <paramref name="threadRootId"/>. The reply
    /// lives in the same <c>messages</c> collection but carries a non-null <see cref="ThreadRootId"/>;
    /// the main-flow query filters these out. Denorm fields (count/participants/lastReplyAt)
    /// remain at zero/empty on the reply itself — they are tracked on the root message only.
    /// </summary>
    public static Message SendAsThreadReply(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        DateTimeOffset createdAtUtc,
        Guid threadRootId,
        IEnumerable<Guid>? mentions = null,
        ReplyTo? replyTo = null,
        ParticipantRole authorRole = ParticipantRole.Member)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientMessageId);

        if (threadRootId == Guid.Empty)
        {
            throw new ArgumentException("threadRootId must be a non-empty GUID.", nameof(threadRootId));
        }

        if (threadRootId == id)
        {
            throw new InvalidOperationException("A thread reply cannot reference itself as the thread root.");
        }

        return new Message(
            id,
            conversationId,
            senderId,
            body ?? string.Empty,
            attachments,
            clientMessageId,
            MessageState.Sent,
            createdAtUtc,
            deliveredAtUtc: null,
            readAtUtc: null,
            editedAtUtc: null,
            editHistory: null,
            deletedAtUtc: null,
            deletedBy: null,
            deleteMode: null,
            hiddenFor: null,
            reactions: null,
            mentions: mentions,
            replyTo: replyTo,
            forwardedFrom: null,
            readBy: null,
            threadRootId: threadRootId,
            threadReplyCount: 0,
            threadParticipants: null,
            threadLastReplyAtUtc: null,
            authorRole: authorRole);
    }

    public static Message Hydrate(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        MessageState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? deliveredAtUtc,
        DateTimeOffset? readAtUtc,
        DateTimeOffset? editedAtUtc = null,
        IEnumerable<EditHistoryEntry>? editHistory = null,
        DateTimeOffset? deletedAtUtc = null,
        Guid? deletedBy = null,
        DeleteMode? deleteMode = null,
        IEnumerable<Guid>? hiddenFor = null,
        IEnumerable<Reaction>? reactions = null,
        IEnumerable<Guid>? mentions = null,
        ReplyTo? replyTo = null,
        ForwardedFrom? forwardedFrom = null,
        IEnumerable<ReadReceipt>? readBy = null,
        Guid? threadRootId = null,
        int threadReplyCount = 0,
        IEnumerable<Guid>? threadParticipants = null,
        DateTimeOffset? threadLastReplyAtUtc = null,
        ParticipantRole authorRole = ParticipantRole.Member)
        => new(
            id,
            conversationId,
            senderId,
            body,
            attachments,
            clientMessageId,
            state,
            createdAtUtc,
            deliveredAtUtc,
            readAtUtc,
            editedAtUtc,
            editHistory,
            deletedAtUtc,
            deletedBy,
            deleteMode,
            hiddenFor,
            reactions,
            mentions,
            replyTo,
            forwardedFrom,
            readBy,
            threadRootId,
            threadReplyCount,
            threadParticipants,
            threadLastReplyAtUtc,
            authorRole);

    public bool MarkDelivered(DateTimeOffset atUtc)
    {
        if (State != MessageState.Sent)
        {
            return false;
        }

        State = MessageState.Delivered;
        DeliveredAtUtc = atUtc;
        return true;
    }

    public bool MarkRead(DateTimeOffset atUtc)
    {
        if (State == MessageState.Read || State == MessageState.Deleted)
        {
            return false;
        }

        DeliveredAtUtc ??= atUtc;
        ReadAtUtc = atUtc;
        State = MessageState.Read;
        return true;
    }

    public bool IsAuthor(Guid userId) => SenderId == userId;

    public bool IsEditableBy(Guid userId, DateTimeOffset nowUtc, TimeSpan ttl)
        => IsAuthor(userId)
           && State != MessageState.Deleted
           && nowUtc - CreatedAtUtc <= ttl;

    public bool IsDeletableForEveryoneBy(Guid userId, DateTimeOffset nowUtc, TimeSpan ttl)
        => IsAuthor(userId)
           && State != MessageState.Deleted
           && nowUtc - CreatedAtUtc <= ttl;

    public bool Edit(string newBody, IReadOnlyList<Guid> validatedMentions, DateTimeOffset atUtc, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(validatedMentions);

        if (State == MessageState.Deleted)
        {
            return false;
        }

        if (atUtc - CreatedAtUtc > ttl)
        {
            return false;
        }

        var safeBody = newBody ?? string.Empty;
        if (string.Equals(Body, safeBody, StringComparison.Ordinal)
            && _mentions.SequenceEqual(validatedMentions))
        {
            return false;
        }

        _editHistory.Add(new EditHistoryEntry(Body, EditedAtUtc ?? CreatedAtUtc));
        Body = safeBody;
        _mentions.Clear();
        _mentions.AddRange(validatedMentions);
        EditedAtUtc = atUtc;
        return true;
    }

    public bool MarkDeletedForEveryone(Guid byUserId, DateTimeOffset atUtc, TimeSpan ttl)
    {
        if (State == MessageState.Deleted)
        {
            return false;
        }

        if (atUtc - CreatedAtUtc > ttl)
        {
            return false;
        }

        State = MessageState.Deleted;
        DeletedAtUtc = atUtc;
        DeletedBy = byUserId;
        // Fully qualified to disambiguate from the property of the same name on this aggregate.
        DeleteMode = BuildingBlocks.Contracts.Integration.Chat.DeleteMode.ForEveryone;
        Body = string.Empty;
        _attachments.Clear();
        _reactions.Clear();
        _mentions.Clear();
        ReplyTo = null;
        ForwardedFrom = null;
        return true;
    }

    public bool MarkDeletedForMe(Guid userId)
    {
        if (_hiddenFor.Contains(userId))
        {
            return false;
        }

        _hiddenFor.Add(userId);
        return true;
    }

    public bool IsHiddenFor(Guid userId) => _hiddenFor.Contains(userId);

    public bool AddReaction(Guid userId, string emoji, DateTimeOffset atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emoji);

        if (State == MessageState.Deleted)
        {
            return false;
        }

        var existingIndex = _reactions.FindIndex(r => r.UserId == userId);
        if (existingIndex >= 0)
        {
            var existing = _reactions[existingIndex];
            if (string.Equals(existing.Emoji, emoji, StringComparison.Ordinal))
            {
                return false;
            }
            _reactions.RemoveAt(existingIndex);
        }

        _reactions.Add(new Reaction(userId, emoji, atUtc));
        return true;
    }

    public bool RemoveReaction(Guid userId, string emoji)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emoji);

        var index = _reactions.FindIndex(r => r.UserId == userId
            && string.Equals(r.Emoji, emoji, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _reactions.RemoveAt(index);
        return true;
    }

    public bool MarkReadBy(Guid userId, DateTimeOffset atUtc)
    {
        if (State == MessageState.Deleted)
        {
            return false;
        }

        if (_readBy.Any(r => r.UserId == userId))
        {
            return false;
        }

        _readBy.Add(new ReadReceipt(userId, atUtc));

        // Preserve existing direct-chat semantics: keep the scalar ReadAt as the first read time.
        if (State != MessageState.Read)
        {
            DeliveredAtUtc ??= atUtc;
            ReadAtUtc = atUtc;
            State = MessageState.Read;
        }

        return true;
    }

    /// <summary>
    /// Records a new thread reply by updating the denormalized counters on this root message.
    /// Increments <see cref="ThreadReplyCount"/>, adds the replier to <see cref="ThreadParticipants"/>
    /// (deduplicated), and refreshes <see cref="ThreadLastReplyAtUtc"/>. Must be called only on
    /// root messages — calling on a reply throws.
    /// </summary>
    public void IncrementThreadDenorm(Guid replierUserId, DateTimeOffset atUtc)
    {
        if (IsThreadReply)
        {
            throw new InvalidOperationException("Thread denorm can only be incremented on root messages.");
        }

        ThreadReplyCount++;
        if (!_threadParticipants.Contains(replierUserId))
        {
            _threadParticipants.Add(replierUserId);
        }
        ThreadLastReplyAtUtc = atUtc;
    }
}
