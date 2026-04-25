using MongoDB.Bson.Serialization.Attributes;
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
        ReadAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(ReadAtUtc.Value, DateTimeKind.Utc)) : null);

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
    };
}
