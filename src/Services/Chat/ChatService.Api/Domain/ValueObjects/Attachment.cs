using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record Attachment(
    Guid MediaAssetId,
    AttachmentType Type,
    Guid? ThumbnailAssetId,
    string FileName,
    long Size,
    string MimeType,
    int? DurationSeconds = null);
