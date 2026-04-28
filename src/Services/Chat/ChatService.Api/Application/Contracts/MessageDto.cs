using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
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
    string ClientMessageId,
    DateTimeOffset? EditedAtUtc = null,
    DateTimeOffset? DeletedAtUtc = null,
    Guid? DeletedBy = null,
    DeleteMode? DeleteMode = null,
    IReadOnlyList<Guid>? Mentions = null,
    ReplyToDto? ReplyTo = null,
    ForwardedFromDto? ForwardedFrom = null,
    IReadOnlyDictionary<string, IReadOnlyList<Guid>>? ReactionsSummary = null,
    Guid? ThreadRootId = null,
    int? ThreadReplyCount = null,
    IReadOnlyList<Guid>? ThreadParticipants = null,
    DateTimeOffset? ThreadLastReplyAtUtc = null,
    ParticipantRole AuthorRole = ParticipantRole.Member)
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
            message.ClientMessageId,
            EditedAtUtc: message.EditedAtUtc,
            DeletedAtUtc: message.DeletedAtUtc,
            DeletedBy: message.DeletedBy,
            DeleteMode: message.DeleteMode,
            Mentions: message.Mentions.Count == 0 ? Array.Empty<Guid>() : message.Mentions.ToList(),
            ReplyTo: message.ReplyTo is { } r ? ReplyToDto.FromDomain(r) : null,
            ForwardedFrom: message.ForwardedFrom is { } f ? ForwardedFromDto.FromDomain(f) : null,
            ReactionsSummary: BuildReactionsSummary(message),
            // Thread fields are surfaced only when set so clients without thread support see a
            // shape identical to the pre-#212 contract. Replies carry ThreadRootId; roots that
            // accumulated replies carry the denorm trio.
            ThreadRootId: message.ThreadRootId,
            ThreadReplyCount: message.ThreadReplyCount > 0 ? message.ThreadReplyCount : null,
            ThreadParticipants: message.ThreadParticipants.Count == 0 ? null : message.ThreadParticipants.ToList(),
            ThreadLastReplyAtUtc: message.ThreadLastReplyAtUtc,
            AuthorRole: message.AuthorRole);
    }

    private static Dictionary<string, IReadOnlyList<Guid>> BuildReactionsSummary(Message message)
    {
        if (message.Reactions.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);
        }

        return message.Reactions
            .GroupBy(r => r.Emoji, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(r => r.UserId).ToList(),
                StringComparer.Ordinal);
    }
}
