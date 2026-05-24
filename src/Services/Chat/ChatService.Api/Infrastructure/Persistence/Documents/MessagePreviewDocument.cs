using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class MessagePreviewDocument
{
    [BsonElement("senderId")]
    public Guid SenderId { get; set; }

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("sentAtUtc")]
    public DateTime SentAtUtc { get; set; }

    [BsonElement("hasAttachments")]
    public bool HasAttachments { get; set; }

    [BsonElement("attachmentFileNames")]
    public List<string> AttachmentFileNames { get; set; } = new();

    public MessagePreview ToDomain()
        => new(
            SenderId,
            Body,
            new DateTimeOffset(DateTime.SpecifyKind(SentAtUtc, DateTimeKind.Utc)),
            HasAttachments,
            AttachmentFileNames);

    public static MessagePreviewDocument FromDomain(MessagePreview preview) => new()
    {
        SenderId = preview.SenderId,
        Body = preview.Body,
        SentAtUtc = preview.SentAtUtc.UtcDateTime,
        HasAttachments = preview.HasAttachments,
        AttachmentFileNames = preview.AttachmentFileNames.ToList(),
    };
}
