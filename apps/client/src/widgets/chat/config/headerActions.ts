import { BellSlashIcon, PushPinIcon, UserCircleIcon } from "@/shared/ui/phosphor";

export const getChatHeaderActions = (callbacks: {
    onOpenProfile: () => void;
    onOpenPinned?: () => void;
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
        icon: BellSlashIcon,
        iconClassName: "text-text-muted",
        label: "Отключить уведомления",
        command: () => console.log("Уведомления..."),
    },
];

export const getSubjectHeaderActions = (callbacks: {
    onOpenMembers: () => void;
    onOpenPinned?: () => void;
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
        icon: BellSlashIcon,
        iconClassName: "text-text-muted",
        label: "Отключить уведомления",
        command: () => console.log("Уведомления..."),
    },
];
