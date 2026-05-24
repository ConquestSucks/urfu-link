using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class MessageDocument
{
    [BsonId]
    public Guid Id { get; set; }

    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement("senderId")]
    public Guid SenderId { get; set; }

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("attachments")]
    public List<AttachmentDocument> Attachments { get; set; } = new();

    [BsonElement("clientMessageId")]
    public string ClientMessageId { get; set; } = string.Empty;

    [BsonElement("state")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public MessageState State { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("deliveredAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? DeliveredAtUtc { get; set; }

    [BsonElement("readAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? ReadAtUtc { get; set; }

    [BsonElement("editedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? EditedAtUtc { get; set; }

    [BsonElement("editHistory")]
    public List<EditHistoryEntryDocument> EditHistory { get; set; } = new();

    [BsonElement("deletedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? DeletedAtUtc { get; set; }

    [BsonElement("deletedBy")]
    [BsonIgnoreIfNull]
    public Guid? DeletedBy { get; set; }

    [BsonElement("deleteMode")]
    [BsonIgnoreIfNull]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public DeleteMode? DeleteMode { get; set; }

    [BsonElement("hiddenFor")]
    public List<Guid> HiddenFor { get; set; } = new();

    [BsonElement("reactions")]
    public List<ReactionDocument> Reactions { get; set; } = new();

    [BsonElement("mentions")]
    public List<Guid> Mentions { get; set; } = new();

    [BsonElement("replyTo")]
    [BsonIgnoreIfNull]
    public ReplyToDocument? ReplyTo { get; set; }

    [BsonElement("forwardedFrom")]
    [BsonIgnoreIfNull]
    public ForwardedFromDocument? ForwardedFrom { get; set; }

    [BsonElement("readBy")]
    public List<ReadReceiptDocument> ReadBy { get; set; } = new();

    [BsonElement("threadRootId")]
    [BsonIgnoreIfNull]
    public Guid? ThreadRootId { get; set; }

    [BsonElement("threadReplyCount")]
    public int ThreadReplyCount { get; set; }

    [BsonElement("threadParticipants")]
    public List<Guid> ThreadParticipants { get; set; } = new();

    [BsonElement("threadLastReplyAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? ThreadLastReplyAtUtc { get; set; }

    /// <summary>
    /// Role of the author at the moment the message was persisted. Default <c>Member</c>
    /// is what legacy documents missing this field deserialise to.
    /// </summary>
    [BsonElement("authorRole")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public ParticipantRole AuthorRole { get; set; }

    public Message ToDomain() => Message.Hydrate(
        Id,
        ConversationId,
        SenderId,
        Body,
        Attachments.Select(a => a.ToDomain()),
        ClientMessageId,
        State,
        new DateTimeOffset(DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc)),
        DeliveredAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(DeliveredAtUtc.Value, DateTimeKind.Utc)) : null,
        ReadAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(ReadAtUtc.Value, DateTimeKind.Utc)) : null,
        EditedAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(EditedAtUtc.Value, DateTimeKind.Utc)) : null,
        EditHistory.Select(e => e.ToDomain()),
        DeletedAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(DeletedAtUtc.Value, DateTimeKind.Utc)) : null,
        DeletedBy,
        DeleteMode,
        HiddenFor,
        Reactions.Select(r => r.ToDomain()),
        Mentions,
        ReplyTo?.ToDomain(),
        ForwardedFrom?.ToDomain(),
        ReadBy.Select(r => r.ToDomain()),
        ThreadRootId,
        ThreadReplyCount,
        ThreadParticipants,
        ThreadLastReplyAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(ThreadLastReplyAtUtc.Value, DateTimeKind.Utc)) : null,
        AuthorRole);

    public static MessageDocument FromDomain(Message message) => new()
    {
        Id = message.Id,
        ConversationId = message.ConversationId,
        SenderId = message.SenderId,
        Body = message.Body,
        Attachments = message.Attachments.Select(AttachmentDocument.FromDomain).ToList(),
        ClientMessageId = message.ClientMessageId,
        State = message.State,
        CreatedAtUtc = message.CreatedAtUtc.UtcDateTime,
        DeliveredAtUtc = message.DeliveredAtUtc?.UtcDateTime,
        ReadAtUtc = message.ReadAtUtc?.UtcDateTime,
        EditedAtUtc = message.EditedAtUtc?.UtcDateTime,
        EditHistory = message.EditHistory.Select(EditHistoryEntryDocument.FromDomain).ToList(),
        DeletedAtUtc = message.DeletedAtUtc?.UtcDateTime,
        DeletedBy = message.DeletedBy,
        DeleteMode = message.DeleteMode,
        HiddenFor = message.HiddenFor.ToList(),
        Reactions = message.Reactions.Select(ReactionDocument.FromDomain).ToList(),
        Mentions = message.Mentions.ToList(),
        ReplyTo = message.ReplyTo is { } r ? ReplyToDocument.FromDomain(r) : null,
        ForwardedFrom = message.ForwardedFrom is { } f ? ForwardedFromDocument.FromDomain(f) : null,
        ReadBy = message.ReadBy.Select(ReadReceiptDocument.FromDomain).ToList(),
        ThreadRootId = message.ThreadRootId,
        ThreadReplyCount = message.ThreadReplyCount,
        ThreadParticipants = message.ThreadParticipants.ToList(),
        ThreadLastReplyAtUtc = message.ThreadLastReplyAtUtc?.UtcDateTime,
        AuthorRole = message.AuthorRole,
    };
}
