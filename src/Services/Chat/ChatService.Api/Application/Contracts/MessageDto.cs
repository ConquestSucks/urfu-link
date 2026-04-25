using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record MessageDto(
    Guid Id,
    string ConversationId,
    Guid SenderId,
    string Body,
    IReadOnlyList<AttachmentDto> Attachments,
    MessageState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? ReadAtUtc,
    string ClientMessageId)
{
    public static MessageDto FromDomain(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.Body,
            message.Attachments.Select(AttachmentDto.FromDomain).ToList(),
            message.State,
            message.CreatedAtUtc,
            message.DeliveredAtUtc,
            message.ReadAtUtc,
            message.ClientMessageId);
    }
}
