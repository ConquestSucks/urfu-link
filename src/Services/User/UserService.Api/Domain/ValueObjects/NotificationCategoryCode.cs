namespace UserService.Api.Domain.ValueObjects;

/// <summary>
/// Numeric codes for notification categories shared with NotificationService.
/// Values must mirror Notification.Domain.Enums.NotificationCategory.
/// </summary>
public static class NotificationCategoryCode
{
    public const int ChatMessageDirect = 1;
    public const int ChatMessageMention = 2;
    public const int ChatMessageDiscipline = 3;
    public const int CallIncoming = 10;
    public const int CallMissed = 11;
    public const int DisciplineAnnouncement = 20;
    public const int DisciplineMaterial = 21;
    public const int DisciplineDeadline = 22;
    public const int SystemMaintenance = 30;
    public const int SystemUpdate = 31;
    public const int AdminChatInvite = 40;
    public const int AdminRoleChanged = 41;

    public static IReadOnlyList<int> All { get; } =
    [
        ChatMessageDirect,
        ChatMessageMention,
        ChatMessageDiscipline,
        CallIncoming,
        CallMissed,
        DisciplineAnnouncement,
        DisciplineMaterial,
        DisciplineDeadline,
        SystemMaintenance,
        SystemUpdate,
        AdminChatInvite,
        AdminRoleChanged,
    ];
}
