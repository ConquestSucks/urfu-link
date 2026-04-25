using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class ReplyToDocument
{
    [BsonElement("messageId")]
    public Guid MessageId { get; set; }

    [BsonElement("senderId")]
    public Guid SenderId { get; set; }

    [BsonElement("preview")]
    public string Preview { get; set; } = string.Empty;

    public ReplyTo ToDomain() => new(MessageId, SenderId, Preview);

    public static ReplyToDocument FromDomain(ReplyTo replyTo) => new()
    {
        MessageId = replyTo.MessageId,
        SenderId = replyTo.SenderId,
        Preview = replyTo.Preview,
    };
}
