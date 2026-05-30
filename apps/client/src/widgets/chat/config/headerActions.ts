import { BellIcon, BellSlashIcon, PushPinIcon, UserCircleIcon } from "@/shared/ui/phosphor";

export const getChatHeaderActions = (callbacks: {
    onOpenProfile: () => void;
    onOpenPinned?: () => void;
    onToggleNotifications: () => void;
    notificationsMuted: boolean;
    notificationsPending?: boolean;
}) => [
    {
        icon: UserCircleIcon,
        iconClassName: "text-brand-400",
        label: "Профиль пользователя",
        command: callbacks.onOpenProfile,
    },
    {
        icon: PushPinIcon,
        iconClassName: "text-warning-500",
        label: "Закрепленные",
        command: callbacks.onOpenPinned,
    },
    { separator: true },
    {
        icon: callbacks.notificationsMuted ? BellIcon : BellSlashIcon,
        iconClassName: "text-text-muted",
        label: callbacks.notificationsMuted ? "Включить уведомления" : "Отключить уведомления",
        command: callbacks.onToggleNotifications,
        disabled: callbacks.notificationsPending,
    },
];

export const getSubjectHeaderActions = (callbacks: {
    onOpenMembers: () => void;
    onOpenPinned?: () => void;
    onToggleNotifications: () => void;
    notificationsMuted: boolean;
    notificationsPending?: boolean;
}) => [
    {
        icon: UserCircleIcon,
        iconClassName: "text-brand-400",
        label: "Участники",
        command: callbacks.onOpenMembers,
    },
    {
        icon: PushPinIcon,
        iconClassName: "text-warning-500",
        label: "Закрепленные",
        command: callbacks.onOpenPinned,
    },
    { separator: true },
    {
        icon: callbacks.notificationsMuted ? BellIcon : BellSlashIcon,
        iconClassName: "text-text-muted",
        label: callbacks.notificationsMuted ? "Включить уведомления" : "Отключить уведомления",
        command: callbacks.onToggleNotifications,
        disabled: callbacks.notificationsPending,
    },
];
