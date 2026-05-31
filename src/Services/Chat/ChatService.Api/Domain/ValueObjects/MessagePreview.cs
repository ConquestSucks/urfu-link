using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record MessagePreview
{
    public MessagePreview(
        Guid senderId,
        string body,
        DateTimeOffset sentAtUtc,
        bool hasAttachments)
        : this(senderId, body, sentAtUtc, hasAttachments, Array.Empty<string>(), Array.Empty<AttachmentType>())
    {
    }

    public MessagePreview(
        Guid senderId,
        string body,
        DateTimeOffset sentAtUtc,
        bool hasAttachments,
        IReadOnlyList<string>? attachmentFileNames)
        : this(senderId, body, sentAtUtc, hasAttachments, attachmentFileNames, Array.Empty<AttachmentType>())
    {
    }

    public MessagePreview(
        Guid senderId,
        string body,
        DateTimeOffset sentAtUtc,
        bool hasAttachments,
        IReadOnlyList<string>? attachmentFileNames,
        IReadOnlyList<AttachmentType>? attachmentTypes)
    {
        SenderId = senderId;
        Body = body;
        SentAtUtc = sentAtUtc;
        HasAttachments = hasAttachments;
        AttachmentFileNames = attachmentFileNames ?? Array.Empty<string>();
        AttachmentTypes = attachmentTypes ?? Array.Empty<AttachmentType>();
    }

    public Guid SenderId { get; init; }

    public string Body { get; init; }

    public DateTimeOffset SentAtUtc { get; init; }

    public bool HasAttachments { get; init; }

    public IReadOnlyList<string> AttachmentFileNames { get; init; }

    public IReadOnlyList<AttachmentType> AttachmentTypes { get; init; }
}
