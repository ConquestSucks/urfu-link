using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record ConversationDto(
    string Id,
    ConversationType Type,
    IReadOnlyList<Guid> Participants,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastMessageAtUtc,
    MessagePreviewDto? LastMessagePreview)
{
    public static ConversationDto FromDomain(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return new ConversationDto(
            conversation.Id,
            conversation.Type,
            conversation.Participants.ToList(),
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc,
            conversation.LastMessagePreview is { } p
                ? new MessagePreviewDto(p.SenderId, p.Body, p.SentAtUtc, p.HasAttachments)
                : null);
    }
}

public sealed record MessagePreviewDto(
    Guid SenderId,
    string Body,
    DateTimeOffset SentAtUtc,
    bool HasAttachments);
