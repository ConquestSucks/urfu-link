using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class EditHistoryEntryDocument
{
    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("editedAtUtc")]
    public DateTime EditedAtUtc { get; set; }

    public EditHistoryEntry ToDomain()
        => new(Body, new DateTimeOffset(DateTime.SpecifyKind(EditedAtUtc, DateTimeKind.Utc)));

    public static EditHistoryEntryDocument FromDomain(EditHistoryEntry entry) => new()
    {
        Body = entry.Body,
        EditedAtUtc = entry.EditedAtUtc.UtcDateTime,
    };
}
