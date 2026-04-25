using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class ReactionDocument
{
    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("emoji")]
    public string Emoji { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public Reaction ToDomain()
        => new(UserId, Emoji, new DateTimeOffset(DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc)));

    public static ReactionDocument FromDomain(Reaction reaction) => new()
    {
        UserId = reaction.UserId,
        Emoji = reaction.Emoji,
        CreatedAtUtc = reaction.CreatedAtUtc.UtcDateTime,
    };
}
