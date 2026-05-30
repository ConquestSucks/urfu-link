using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record SystemCallDto(
    Guid CallId,
    CallType CallType,
    SystemCallStatus Status,
    Guid CallerId,
    TimeSpan? Duration,
    CallEndReason? EndReason)
{
    public static SystemCallDto FromDomain(SystemCallInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return new SystemCallDto(
            info.CallId,
            info.CallType,
            info.Status,
            info.CallerId,
            info.Duration,
            info.EndReason);
    }
}
