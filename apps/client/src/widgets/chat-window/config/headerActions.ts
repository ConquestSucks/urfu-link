import { BellSlashIcon, MagnifyingGlassIcon, ProhibitIcon, PushPinIcon, TrashIcon, UserCircleIcon, } from "@/shared/ui/phosphor";
export const getChatHeaderActions = (callbacks: {
    onOpenProfile: () => void;
}) => [
    {
        icon: UserCircleIcon,
        iconClassName: "text-brand-400",
        label: "Профиль пользователя",
        command: callbacks.onOpenProfile,
    },
    {
        icon: MagnifyingGlassIcon,
        iconClassName: "text-success-500",
        label: "Поиск по сообщениям",
        command: () => console.log("Ищем..."),
    },
    {
        icon: PushPinIcon,
        iconClassName: "text-warning-500",
        label: "Закрепленные",
        command: () => console.log("Закрепленные..."),
    },
    { separator: true },
    {
        icon: BellSlashIcon,
        iconClassName: "text-text-muted",
        label: "Отключить уведомления",
        command: () => console.log("Уведомления..."),
    },
    {
        icon: TrashIcon,
        iconClassName: "text-warning-600",
        label: "Очистить историю",
        command: () => console.log("Чистим..."),
    },
    { separator: true },
    {
        icon: ProhibitIcon,
        iconClassName: "text-danger-300",
        label: "Заблокировать",
        command: () => console.log("Блокируем..."),
        danger: true,
    },
];
export const getSubjectHeaderActions = (callbacks: {
    onOpenMembers: () => void;
}) => [
    {
        icon: UserCircleIcon,
        iconClassName: "text-brand-400",
        label: "Участники",
        command: callbacks.onOpenMembers,
    },
    {
        icon: MagnifyingGlassIcon,
        iconClassName: "text-success-500",
        label: "Поиск по сообщениям",
        command: () => console.log("Ищем..."),
    },
    {
        icon: PushPinIcon,
        iconClassName: "text-warning-500",
        label: "Закрепленные",
        command: () => console.log("Закрепленные..."),
    },
    { separator: true },
    {
        icon: BellSlashIcon,
        iconClassName: "text-text-muted",
        label: "Отключить уведомления",
        command: () => console.log("Уведомления..."),
    },
];
