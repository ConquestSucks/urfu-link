namespace UserService.Api.Application.Contracts.Requests;

public sealed record UpdatePrivacyRequest(bool ShowOnlineStatus, bool ShowLastVisitTime);
