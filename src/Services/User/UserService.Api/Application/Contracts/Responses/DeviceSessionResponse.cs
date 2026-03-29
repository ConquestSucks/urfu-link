namespace UserService.Api.Application.Contracts.Responses;

public sealed record DeviceSessionResponse(
    string SessionId,
    string? IpAddress,
    DateTimeOffset LastAccess,
    string? Browser,
    string? Os,
    bool IsCurrent);
