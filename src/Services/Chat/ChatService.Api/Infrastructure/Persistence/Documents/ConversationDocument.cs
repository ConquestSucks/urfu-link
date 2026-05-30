using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class ConversationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("type")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public ConversationType Type { get; set; }

    [BsonElement("participants")]
    public List<Guid> Participants { get; set; } = new();

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("lastMessageAtUtc")]
    public DateTime LastMessageAtUtc { get; set; }

    [BsonElement("lastMessagePreview")]
    [BsonIgnoreIfNull]
    public MessagePreviewDocument? LastMessagePreview { get; set; }

    [BsonElement("pinnedMessageIds")]
    public List<Guid> PinnedMessageIds { get; set; } = new();

    /// <summary>
    /// Map of user id -> role. Persisted as a Bson array of {userId, role} entries to
    /// avoid GUID-key compatibility issues across drivers. Always present on documents
    /// produced after the discipline rollout; may be absent on older direct rows, in
    /// which case ParticipantRole.Member is implied.
    /// </summary>
    [BsonElement("participantRoles")]
    [BsonIgnoreIfNull]
    public List<ParticipantRoleEntry>? ParticipantRoles { get; set; }

    [BsonElement("disciplineId")]
    [BsonIgnoreIfNull]
    public Guid? DisciplineId { get; set; }

    [BsonElement("disciplineChatKind")]
    [BsonIgnoreIfNull]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public DisciplineChatKind? DisciplineChatKind { get; set; }

    [BsonElement("disciplineSubgroupId")]
    [BsonIgnoreIfNull]
    public Guid? DisciplineSubgroupId { get; set; }

    [BsonElement("disciplineTitle")]
    [BsonIgnoreIfNull]
    public string? DisciplineTitle { get; set; }

    [BsonElement("disciplineSubgroupName")]
    [BsonIgnoreIfNull]
    public string? DisciplineSubgroupName { get; set; }

    [BsonElement("archivedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? ArchivedAtUtc { get; set; }

    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title { get; set; }

    [BsonElement("coverAssetId")]
    [BsonIgnoreIfNull]
    public Guid? CoverAssetId { get; set; }

    [BsonElement("groupSubtype")]
    [BsonIgnoreIfNull]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public GroupSubtype? GroupSubtype { get; set; }

    [BsonElement("isAnnouncementOnly")]
    public bool IsAnnouncementOnly { get; set; }

    public Conversation ToDomain() => Conversation.Hydrate(
        Id,
        Type,
        Participants,
        new DateTimeOffset(DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(LastMessageAtUtc, DateTimeKind.Utc)),
        LastMessagePreview?.ToDomain(),
        PinnedMessageIds,
        ParticipantRoles?.ToDictionary(e => e.UserId, e => e.Role),
        DisciplineId,
        DisciplineChatKind,
        DisciplineSubgroupId,
        DisciplineTitle,
        DisciplineSubgroupName,
        ArchivedAtUtc is { } archived
            ? new DateTimeOffset(DateTime.SpecifyKind(archived, DateTimeKind.Utc))
            : null,
        Title,
        CoverAssetId,
        GroupSubtype,
        IsAnnouncementOnly);

    public static ConversationDocument FromDomain(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        return new ConversationDocument
        {
            Id = conversation.Id,
            Type = conversation.Type,
            Participants = conversation.Participants.ToList(),
            CreatedAtUtc = conversation.CreatedAtUtc.UtcDateTime,
            LastMessageAtUtc = conversation.LastMessageAtUtc.UtcDateTime,
            LastMessagePreview = conversation.LastMessagePreview is { } p
                ? MessagePreviewDocument.FromDomain(p)
                : null,
            PinnedMessageIds = conversation.PinnedMessageIds.ToList(),
            ParticipantRoles = conversation.ParticipantRoles.Count == 0
                ? null
                : conversation.ParticipantRoles
                    .Select(kv => new ParticipantRoleEntry(kv.Key, kv.Value))
                    .ToList(),
            DisciplineId = conversation.DisciplineId,
            DisciplineChatKind = conversation.DisciplineChatKind,
            DisciplineSubgroupId = conversation.DisciplineSubgroupId,
            DisciplineTitle = conversation.DisciplineTitle,
            DisciplineSubgroupName = conversation.DisciplineSubgroupName,
            ArchivedAtUtc = conversation.ArchivedAtUtc?.UtcDateTime,
            Title = conversation.Title,
            CoverAssetId = conversation.CoverAssetId,
            GroupSubtype = conversation.GroupSubtype,
            IsAnnouncementOnly = conversation.IsAnnouncementOnly,
        };
    }
}

internal sealed class ParticipantRoleEntry
{
    public ParticipantRoleEntry()
    {
    }

    public ParticipantRoleEntry(Guid userId, ParticipantRole role)
    {
        UserId = userId;
        Role = role;
    }

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("role")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public ParticipantRole Role { get; set; }
}
