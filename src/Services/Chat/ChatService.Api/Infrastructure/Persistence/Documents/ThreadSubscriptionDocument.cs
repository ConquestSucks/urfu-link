using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class ThreadSubscriptionDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("rootMessageId")]
    public Guid RootMessageId { get; set; }

    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("reason")]
    [BsonRepresentation(BsonType.String)]
    public ThreadSubscriptionReason Reason { get; set; }

    [BsonElement("subscribedAtUtc")]
    public DateTime SubscribedAtUtc { get; set; }

    [BsonElement("lastActivityAtUtc")]
    public DateTime LastActivityAtUtc { get; set; }

    public ThreadSubscription ToDomain() => ThreadSubscription.Hydrate(
        RootMessageId,
        UserId,
        Reason,
        new DateTimeOffset(DateTime.SpecifyKind(SubscribedAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(LastActivityAtUtc, DateTimeKind.Utc)));

    public static ThreadSubscriptionDocument FromDomain(ThreadSubscription subscription) => new()
    {
        Id = ObjectId.GenerateNewId(),
        RootMessageId = subscription.RootMessageId,
        UserId = subscription.UserId,
        Reason = subscription.Reason,
        SubscribedAtUtc = subscription.SubscribedAtUtc.UtcDateTime,
        LastActivityAtUtc = subscription.LastActivityAtUtc.UtcDateTime,
    };
}
