using MongoDB.Bson.Serialization.Attributes;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

internal sealed class SystemCallInfoDocument
{
    [BsonElement("callId")]
    public Guid CallId { get; set; }

    [BsonElement("callType")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public CallType CallType { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public SystemCallStatus Status { get; set; }

    [BsonElement("callerId")]
    public Guid CallerId { get; set; }

    [BsonElement("durationMs")]
    [BsonIgnoreIfNull]
    public long? DurationMs { get; set; }

    [BsonElement("endReason")]
    [BsonIgnoreIfNull]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public CallEndReason? EndReason { get; set; }

    public SystemCallInfo ToDomain()
        => new(
            CallId,
            CallType,
            Status,
            CallerId,
            DurationMs.HasValue ? TimeSpan.FromMilliseconds(DurationMs.Value) : null,
            EndReason);

    public static SystemCallInfoDocument FromDomain(SystemCallInfo info)
        => new()
        {
            CallId = info.CallId,
            CallType = info.CallType,
            Status = info.Status,
            CallerId = info.CallerId,
            DurationMs = info.Duration.HasValue ? (long)info.Duration.Value.TotalMilliseconds : null,
            EndReason = info.EndReason,
        };
}
