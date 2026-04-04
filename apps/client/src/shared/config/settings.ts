import {
    BellIcon,
    DeviceMobileIcon,
    LockIcon,
    UserIcon,
    VideoCameraIcon,
} from "@/shared/ui/phosphor";

export const SETTINGS_ITEMS = [
    {
        icon: UserIcon,
        label: "Аккаунт",
        description: "Управление данными вашего аккаунта",
        key: "account",
    },
    {
        icon: LockIcon,
        label: "Приватность",
        description: "Управление приватностью и безопасностью",
        key: "privacy",
    },
    {
        icon: DeviceMobileIcon,
        label: "Устройства",
        description: "Управление устройствами с доступом к аккаунту",
        key: "devices",
    },
    {
        icon: BellIcon,
        label: "Уведомления",
        description: "Настройка уведомлений о сообщениях и событиях",
        key: "notifications",
    },
    {
        icon: VideoCameraIcon,
        label: "Звук и видео",
        description: "Настройка параметров звука и видео для звонков",
        key: "media",
    },
];
