import { BellSlashIcon, MagnifyingGlassIcon, ProhibitIcon, PushPinIcon, TrashIcon, UserCircleIcon, } from "phosphor-react-native";
export const getChatHeaderActions = (callbacks: {
    onOpenProfile: () => void;
}) => [
    {
        icon: UserCircleIcon,
        iconColor: "#51A2FF",
        label: "Профиль пользователя",
        command: callbacks.onOpenProfile,
    },
    {
        icon: MagnifyingGlassIcon,
        iconColor: "#00D492",
        label: "Поиск по сообщениям",
        command: () => console.log("Ищем..."),
    },
    {
        icon: PushPinIcon,
        iconColor: "#FFB900",
        label: "Закрепленные",
        command: () => console.log("Закрепленные..."),
    },
    { separator: true },
    {
        icon: BellSlashIcon,
        iconColor: "#90A1B9",
        label: "Отключить уведомления",
        command: () => console.log("Уведомления..."),
    },
    {
        icon: TrashIcon,
        iconColor: "#FF8904",
        label: "Очистить историю",
        command: () => console.log("Чистим..."),
    },
    { separator: true },
    {
        icon: ProhibitIcon,
        iconColor: "#FF637E",
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
        iconColor: "#51A2FF",
        label: "Участники",
        command: callbacks.onOpenMembers,
    },
    {
        icon: MagnifyingGlassIcon,
        iconColor: "#00D492",
        label: "Поиск по сообщениям",
        command: () => console.log("Ищем..."),
    },
    {
        icon: PushPinIcon,
        iconColor: "#FFB900",
        label: "Закрепленные",
        command: () => console.log("Закрепленные..."),
    },
    { separator: true },
    {
        icon: BellSlashIcon,
        iconColor: "#90A1B9",
        label: "Отключить уведомления",
        command: () => console.log("Уведомления..."),
    },
];
