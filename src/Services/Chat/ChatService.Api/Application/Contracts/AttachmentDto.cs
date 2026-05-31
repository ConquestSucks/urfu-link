using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record AttachmentDto(
    Guid MediaAssetId,
    AttachmentType Type,
    Guid? ThumbnailAssetId,
    string FileName,
    long Size,
    string MimeType,
    int? DurationSeconds = null)
{
    public static AttachmentDto FromDomain(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return new AttachmentDto(
            attachment.MediaAssetId,
            attachment.Type,
            attachment.ThumbnailAssetId,
            attachment.FileName,
            attachment.Size,
            attachment.MimeType,
            attachment.DurationSeconds);
    }

    public Attachment ToDomain() => new(
        MediaAssetId,
        Type,
        ThumbnailAssetId,
        FileName,
        Size,
        MimeType,
        DurationSeconds);
}
