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

    public Conversation ToDomain() => Conversation.Hydrate(
        Id,
        Type,
        Participants,
        new DateTimeOffset(DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(LastMessageAtUtc, DateTimeKind.Utc)),
        LastMessagePreview?.ToDomain());

    public static ConversationDocument FromDomain(Conversation conversation) => new()
    {
        Id = conversation.Id,
        Type = conversation.Type,
        Participants = conversation.Participants.ToList(),
        CreatedAtUtc = conversation.CreatedAtUtc.UtcDateTime,
        LastMessageAtUtc = conversation.LastMessageAtUtc.UtcDateTime,
        LastMessagePreview = conversation.LastMessagePreview is { } p ? MessagePreviewDocument.FromDomain(p) : null,
    };
}
