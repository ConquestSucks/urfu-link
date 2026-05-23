namespace UserService.Api.Application.Contracts.Responses;

public sealed record SearchUsersResponse(
    IReadOnlyList<SearchUserDto> Items,
    bool HasMore);

public sealed record SearchUserDto(
    Guid Id,
    string DisplayName,
    string Username,
    string? AvatarUrl);
