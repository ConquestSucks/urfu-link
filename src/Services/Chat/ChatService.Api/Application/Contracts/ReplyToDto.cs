using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record ReplyToDto(
    Guid MessageId,
    Guid SenderId,
    string Preview)
{
    public static ReplyToDto FromDomain(ReplyTo replyTo)
    {
        ArgumentNullException.ThrowIfNull(replyTo);
        return new ReplyToDto(replyTo.MessageId, replyTo.SenderId, replyTo.Preview);
    }
}
