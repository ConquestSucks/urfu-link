using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Domain;

public sealed record NotificationDescriptor(
    string Type,
    NotificationCategory Category,
    NotificationSeverity DefaultSeverity,
    string Icon,
    bool Groupable,
    TimeSpan Retention,
    IReadOnlyList<NotificationAction> DefaultActions);

public static class NotificationCatalog
{
    private static readonly IReadOnlyDictionary<NotificationCategory, NotificationDescriptor> ByCategory =
        new Dictionary<NotificationCategory, NotificationDescriptor>
        {
            [NotificationCategory.ChatMessageDirect] = Descriptor(
                "chat.message.direct",
                NotificationCategory.ChatMessageDirect,
                NotificationSeverity.Normal,
                "chat-circle",
                groupable: true),
            [NotificationCategory.ChatMessageMention] = Descriptor(
                "chat.mention",
                NotificationCategory.ChatMessageMention,
                NotificationSeverity.High,
                "at",
                groupable: true),
            [NotificationCategory.ChatMessageDiscipline] = Descriptor(
                "chat.message.discipline",
                NotificationCategory.ChatMessageDiscipline,
                NotificationSeverity.Normal,
                "chat-circle-text",
                groupable: true),
            [NotificationCategory.CallIncoming] = Descriptor(
                "call.incoming",
                NotificationCategory.CallIncoming,
                NotificationSeverity.Urgent,
                "phone-call",
                groupable: false),
            [NotificationCategory.CallMissed] = Descriptor(
                "call.missed",
                NotificationCategory.CallMissed,
                NotificationSeverity.High,
                "phone-x",
                groupable: true),
            [NotificationCategory.DisciplineAnnouncement] = Descriptor(
                "discipline.announcement",
                NotificationCategory.DisciplineAnnouncement,
                NotificationSeverity.Normal,
                "megaphone",
                groupable: true),
            [NotificationCategory.DisciplineMaterial] = Descriptor(
                "discipline.material",
                NotificationCategory.DisciplineMaterial,
                NotificationSeverity.Normal,
                "file-text",
                groupable: true),
            [NotificationCategory.DisciplineDeadline] = Descriptor(
                "discipline.deadline",
                NotificationCategory.DisciplineDeadline,
                NotificationSeverity.High,
                "clock-countdown",
                groupable: true),
            [NotificationCategory.SystemMaintenance] = Descriptor(
                "system.maintenance",
                NotificationCategory.SystemMaintenance,
                NotificationSeverity.High,
                "warning",
                groupable: true),
            [NotificationCategory.SystemUpdate] = Descriptor(
                "system.update",
                NotificationCategory.SystemUpdate,
                NotificationSeverity.Normal,
                "sparkle",
                groupable: true),
            [NotificationCategory.AdminChatInvite] = Descriptor(
                "admin.chat.invite",
                NotificationCategory.AdminChatInvite,
                NotificationSeverity.High,
                "user-plus",
                groupable: false),
            [NotificationCategory.AdminRoleChanged] = Descriptor(
                "admin.role.changed",
                NotificationCategory.AdminRoleChanged,
                NotificationSeverity.High,
                "shield-check",
                groupable: true),
        };

    private static readonly Dictionary<string, NotificationDescriptor> ByType =
        ByCategory.Values.ToDictionary(d => d.Type, StringComparer.Ordinal);

    public static NotificationDescriptor GetByCategory(NotificationCategory category)
        => ByCategory.TryGetValue(category, out var descriptor)
            ? descriptor
            : Descriptor(
                $"category.{(int)category}",
                category,
                NotificationSeverity.Normal,
                "bell",
                groupable: false);

    public static bool TryGet(string type, out NotificationDescriptor descriptor)
        => ByType.TryGetValue(type, out descriptor!);

    private static NotificationDescriptor Descriptor(
        string type,
        NotificationCategory category,
        NotificationSeverity severity,
        string icon,
        bool groupable)
        => new(
            type,
            category,
            severity,
            icon,
            groupable,
            Retention: TimeSpan.FromDays(90),
            DefaultActions:
            [
                new NotificationAction("open", "Открыть", "deep-link", null),
                new NotificationAction("done", "Готово", "state", null),
            ]);
}
