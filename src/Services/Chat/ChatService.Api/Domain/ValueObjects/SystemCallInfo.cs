using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.ValueObjects;

public sealed record SystemCallInfo(
    Guid CallId,
    CallType CallType,
    SystemCallStatus Status,
    Guid CallerId,
    TimeSpan? Duration,
    CallEndReason? EndReason);
