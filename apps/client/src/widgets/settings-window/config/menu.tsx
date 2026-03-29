import { ManageAccount } from "@/features/manage-account";
import { ManageDevices } from "@/features/manage-devices";
import { ManageMedia } from "@/features/manage-media";
import { ManageNotifications } from "@/features/manage-notifications";
import { ManagePrivacy } from "@/features/manage-privacy";
import { BellIcon, DeviceMobileIcon, LockIcon, UserIcon, VideoCameraIcon, } from "phosphor-react-native";
export const MENU_ITEMS = [
    {
        icon: UserIcon,
        label: "Аккаунт",
        description: "Управление данными вашего аккаунта",
        key: "account",
        component: () => <ManageAccount />,
    },
    {
        icon: LockIcon,
        label: "Приватность",
        description: "Управление приватностью и безопасностью",
        key: "privacy",
        component: () => <ManagePrivacy />,
    },
    {
        icon: DeviceMobileIcon,
        label: "Устройства",
        description: "Управление устройствами с доступом к аккаунту",
        key: "devices",
        component: () => <ManageDevices />,
    },
    {
        icon: BellIcon,
        label: "Уведомления",
        description: "Настройка уведомлений о сообщениях и событиях",
        key: "notifications",
        component: () => <ManageNotifications />,
    },
    {
        icon: VideoCameraIcon,
        label: "Звук и видео",
        description: "Настройка параметров звука и видео для звонков",
        key: "sound-video",
        component: () => <ManageMedia />,
    },
];
