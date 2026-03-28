namespace UserService.Api.Application.Contracts.Requests;

public sealed record UpdateNotificationsRequest(
    bool NewMessages,
    bool NotificationSound,
    bool DisciplineChatMessages,
    bool Mentions);
