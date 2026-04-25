using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class ReadReceiptDocument
{
    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("readAtUtc")]
    public DateTime ReadAtUtc { get; set; }

    public ReadReceipt ToDomain()
        => new(UserId, new DateTimeOffset(DateTime.SpecifyKind(ReadAtUtc, DateTimeKind.Utc)));

    public static ReadReceiptDocument FromDomain(ReadReceipt receipt) => new()
    {
        UserId = receipt.UserId,
        ReadAtUtc = receipt.ReadAtUtc.UtcDateTime,
    };
}
