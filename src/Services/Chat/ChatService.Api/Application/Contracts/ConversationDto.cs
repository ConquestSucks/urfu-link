using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record ConversationDto(
    string Id,
    ConversationType Type,
    IReadOnlyList<Guid> Participants,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastMessageAtUtc,
    MessagePreviewDto? LastMessagePreview,
    IReadOnlyList<Guid>? PinnedMessageIds = null,
    string? Title = null,
    Guid? CoverAssetId = null,
    Guid? DisciplineId = null,
    GroupSubtype? GroupSubtype = null,
    bool IsAnnouncementOnly = false,
    DateTimeOffset? ArchivedAtUtc = null,
    IReadOnlyDictionary<Guid, ParticipantRole>? ParticipantRoles = null)
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
                : null,
            PinnedMessageIds: conversation.PinnedMessageIds.Count == 0
                ? Array.Empty<Guid>()
                : conversation.PinnedMessageIds.ToList(),
            Title: conversation.Title,
            CoverAssetId: conversation.CoverAssetId,
            DisciplineId: conversation.DisciplineId,
            GroupSubtype: conversation.GroupSubtype,
            IsAnnouncementOnly: conversation.IsAnnouncementOnly,
            ArchivedAtUtc: conversation.ArchivedAtUtc,
            ParticipantRoles: conversation.ParticipantRoles.Count == 0
                ? null
                : conversation.ParticipantRoles.ToDictionary(kv => kv.Key, kv => kv.Value));
    }
}

public sealed record MessagePreviewDto(
    Guid SenderId,
    string Body,
    DateTimeOffset SentAtUtc,
    bool HasAttachments);
