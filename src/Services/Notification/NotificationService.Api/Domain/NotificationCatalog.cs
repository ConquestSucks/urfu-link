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
            [NotificationCategory.ChatThreadReply] = Descriptor(
                "chat.thread.reply",
                NotificationCategory.ChatThreadReply,
                NotificationSeverity.Normal,
                "chats-circle",
                groupable: true),
            [NotificationCategory.ChatReplyToMe] = Descriptor(
                "chat.reply_to_me",
                NotificationCategory.ChatReplyToMe,
                NotificationSeverity.High,
                "arrow-bend-up-left",
                groupable: true),
            [NotificationCategory.ChatReaction] = Descriptor(
                "chat.reaction.added",
                NotificationCategory.ChatReaction,
                NotificationSeverity.Low,
                "smiley",
                groupable: true),
            [NotificationCategory.ChatMessagePinned] = Descriptor(
                "chat.message.pinned",
                NotificationCategory.ChatMessagePinned,
                NotificationSeverity.Normal,
                "push-pin",
                groupable: true),
            [NotificationCategory.ChatParticipantChanged] = Descriptor(
                "chat.participant.changed",
                NotificationCategory.ChatParticipantChanged,
                NotificationSeverity.Normal,
                "users-three",
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
            [NotificationCategory.DisciplineEnrollment] = Descriptor(
                "discipline.enrollment",
                NotificationCategory.DisciplineEnrollment,
                NotificationSeverity.Normal,
                "student",
                groupable: true),
            [NotificationCategory.DisciplineUnenrollment] = Descriptor(
                "discipline.unenrollment",
                NotificationCategory.DisciplineUnenrollment,
                NotificationSeverity.Normal,
                "user-minus",
                groupable: true),
            [NotificationCategory.DisciplineUpdated] = Descriptor(
                "discipline.updated",
                NotificationCategory.DisciplineUpdated,
                NotificationSeverity.Normal,
                "pencil-simple",
                groupable: true),
            [NotificationCategory.DisciplineDeleted] = Descriptor(
                "discipline.deleted",
                NotificationCategory.DisciplineDeleted,
                NotificationSeverity.High,
                "trash",
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
            [NotificationCategory.MediaAccessGranted] = Descriptor(
                "media.access.granted",
                NotificationCategory.MediaAccessGranted,
                NotificationSeverity.Normal,
                "lock-open",
                groupable: true),
            [NotificationCategory.MediaAccessRevoked] = Descriptor(
                "media.access.revoked",
                NotificationCategory.MediaAccessRevoked,
                NotificationSeverity.Normal,
                "lock",
                groupable: true),
            [NotificationCategory.MediaUploadProcessed] = Descriptor(
                "media.upload.processed",
                NotificationCategory.MediaUploadProcessed,
                NotificationSeverity.Normal,
                "cloud-check",
                groupable: true),
            [NotificationCategory.MediaAssetDeleted] = Descriptor(
                "media.asset.deleted",
                NotificationCategory.MediaAssetDeleted,
                NotificationSeverity.High,
                "file-x",
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
