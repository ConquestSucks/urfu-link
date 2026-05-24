namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record MessagePreview
{
    public MessagePreview(
        Guid senderId,
        string body,
        DateTimeOffset sentAtUtc,
        bool hasAttachments)
        : this(senderId, body, sentAtUtc, hasAttachments, Array.Empty<string>())
    {
    }

    public MessagePreview(
        Guid senderId,
        string body,
        DateTimeOffset sentAtUtc,
        bool hasAttachments,
        IReadOnlyList<string>? attachmentFileNames)
    {
        SenderId = senderId;
        Body = body;
        SentAtUtc = sentAtUtc;
        HasAttachments = hasAttachments;
        AttachmentFileNames = attachmentFileNames ?? Array.Empty<string>();
    }

    public Guid SenderId { get; init; }

    public string Body { get; init; }

    public DateTimeOffset SentAtUtc { get; init; }

    public bool HasAttachments { get; init; }

    public IReadOnlyList<string> AttachmentFileNames { get; init; }
}
