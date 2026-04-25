using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class AttachmentDocument
{
    [BsonElement("mediaAssetId")]
    public Guid MediaAssetId { get; set; }

    [BsonElement("type")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public AttachmentType Type { get; set; }

    [BsonElement("thumbnailAssetId")]
    [BsonIgnoreIfNull]
    public Guid? ThumbnailAssetId { get; set; }

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("size")]
    public long Size { get; set; }

    [BsonElement("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    public Attachment ToDomain() => new(MediaAssetId, Type, ThumbnailAssetId, FileName, Size, MimeType);

    public static AttachmentDocument FromDomain(Attachment attachment) => new()
    {
        MediaAssetId = attachment.MediaAssetId,
        Type = attachment.Type,
        ThumbnailAssetId = attachment.ThumbnailAssetId,
        FileName = attachment.FileName,
        Size = attachment.Size,
        MimeType = attachment.MimeType,
    };
}
