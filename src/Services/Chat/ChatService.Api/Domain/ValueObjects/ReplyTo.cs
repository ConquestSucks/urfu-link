namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record ReplyTo(
    Guid MessageId,
    Guid SenderId,
    string Preview)
{
    public const int MaxPreviewLength = 100;

    public static ReplyTo Create(Guid messageId, Guid senderId, string body)
        => new(messageId, senderId, BuildPreview(body));

    private static string BuildPreview(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        if (body.Length <= MaxPreviewLength)
        {
            return body;
        }

        // Avoid splitting a UTF-16 surrogate pair when truncating.
        var cutoff = char.IsHighSurrogate(body[MaxPreviewLength - 1])
            ? MaxPreviewLength - 1
            : MaxPreviewLength;
        return body[..cutoff];
    }
}
