namespace UserService.Api.Domain.ValueObjects;

public sealed record NotificationSettings(
    bool NewMessages,
    bool NotificationSound,
    bool DisciplineChatMessages,
    bool Mentions)
{
    public static readonly NotificationSettings Default = new(true, true, true, true);
}
