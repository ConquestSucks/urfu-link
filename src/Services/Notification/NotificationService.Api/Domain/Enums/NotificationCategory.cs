namespace Urfu.Link.Services.Notification.Domain.Enums;

public enum NotificationCategory
{
    ChatMessageDirect = 1,
    ChatMessageMention = 2,
    ChatMessageDiscipline = 3,
    CallIncoming = 10,
    CallMissed = 11,
    DisciplineAnnouncement = 20,
    DisciplineMaterial = 21,
    DisciplineDeadline = 22,
    SystemMaintenance = 30,
    SystemUpdate = 31,
    AdminChatInvite = 40,
    AdminRoleChanged = 41,
}
