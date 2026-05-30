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
    DisciplineChatKind? DisciplineChatKind = null,
    Guid? DisciplineSubgroupId = null,
    string? DisciplineTitle = null,
    string? DisciplineSubgroupName = null,
    bool IsAnnouncementOnly = false,
    DateTimeOffset? ArchivedAtUtc = null,
    IReadOnlyDictionary<Guid, ParticipantRole>? ParticipantRoles = null,
    ConversationCapabilitiesDto? Capabilities = null,
    int? UnreadCount = null)
{
    public static ConversationDto FromDomain(Conversation conversation, Guid? callerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return new ConversationDto(
            conversation.Id,
            conversation.Type,
            conversation.Participants.ToList(),
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc,
            conversation.LastMessagePreview is { } p
                ? new MessagePreviewDto(p.SenderId, p.Body, p.SentAtUtc, p.HasAttachments, p.AttachmentFileNames)
                : null,
            PinnedMessageIds: conversation.PinnedMessageIds.Count == 0
                ? Array.Empty<Guid>()
                : conversation.PinnedMessageIds.ToList(),
            Title: conversation.Title,
            CoverAssetId: conversation.CoverAssetId,
            DisciplineId: conversation.DisciplineId,
            GroupSubtype: conversation.GroupSubtype,
            DisciplineChatKind: conversation.DisciplineChatKind,
            DisciplineSubgroupId: conversation.DisciplineSubgroupId,
            DisciplineTitle: conversation.DisciplineTitle,
            DisciplineSubgroupName: conversation.DisciplineSubgroupName,
            IsAnnouncementOnly: conversation.IsAnnouncementOnly,
            ArchivedAtUtc: conversation.ArchivedAtUtc,
            ParticipantRoles: conversation.ParticipantRoles.Count == 0
                ? null
                : conversation.ParticipantRoles.ToDictionary(kv => kv.Key, kv => kv.Value),
            Capabilities: ConversationCapabilitiesDto.FromDomain(conversation, callerUserId));
    }

    public ConversationDto WithUnreadCount(int unreadCount)
        => this with { UnreadCount = unreadCount };

    public ConversationDto WithLastMessageMetadata(Message? message)
    {
        if (LastMessagePreview is null || message is null || !string.Equals(message.ConversationId, Id, StringComparison.Ordinal))
        {
            return this;
        }

        return this with
        {
            LastMessagePreview = LastMessagePreview with
            {
                MessageId = message.Id,
                ReadAtUtc = message.ReadAtUtc,
                AttachmentFileNames = message.Attachments.Select(a => a.FileName).ToList(),
            },
        };
    }
}

public sealed record ConversationCapabilitiesDto(bool CanStartGroupCall)
{
    public static ConversationCapabilitiesDto FromDomain(Conversation conversation, Guid? callerUserId)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var canStartGroupCall = callerUserId.HasValue
            && conversation.Type == ConversationType.Group
            && conversation.GroupSubtype == GroupSubtype.Discipline
            && conversation.DisciplineChatKind == DisciplineChatKind.Subgroup
            && conversation.RoleOf(callerUserId.Value) == ParticipantRole.Teacher;
        return new ConversationCapabilitiesDto(canStartGroupCall);
    }
}

public sealed record MessagePreviewDto(
    Guid SenderId,
    string Body,
    DateTimeOffset SentAtUtc,
    bool HasAttachments,
    IReadOnlyList<string> AttachmentFileNames,
    Guid? MessageId = null,
    DateTimeOffset? ReadAtUtc = null);
