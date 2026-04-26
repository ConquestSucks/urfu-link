using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class ForwardedFromDocument
{
    [BsonElement("originalSenderId")]
    public Guid OriginalSenderId { get; set; }

    [BsonElement("originalSentAtUtc")]
    public DateTime OriginalSentAtUtc { get; set; }

    [BsonElement("originalConversationId")]
    [BsonIgnoreIfNull]
    public string? OriginalConversationId { get; set; }

    public ForwardedFrom ToDomain()
        => new(OriginalSenderId,
            new DateTimeOffset(DateTime.SpecifyKind(OriginalSentAtUtc, DateTimeKind.Utc)),
            OriginalConversationId);

    public static ForwardedFromDocument FromDomain(ForwardedFrom forwardedFrom) => new()
    {
        OriginalSenderId = forwardedFrom.OriginalSenderId,
        OriginalSentAtUtc = forwardedFrom.OriginalSentAtUtc.UtcDateTime,
        OriginalConversationId = forwardedFrom.OriginalConversationId,
    };
}
