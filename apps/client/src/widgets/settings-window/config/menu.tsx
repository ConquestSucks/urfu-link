import { ManageAccount } from "@/features/manage-account";
import { ManageDevices } from "@/features/manage-devices";
import { ManageMedia } from "@/features/manage-media";
import { ManageNotifications } from "@/features/manage-notifications";
import { ManagePrivacy } from "@/features/manage-privacy";
import { Bell, Lock, Smartphone, User, Video } from "lucide-react-native";

export const MENU_ITEMS = [
  {
    icon: User,
    label: "Аккаунт",
    description: "Управление данными вашего аккаунта",
    key: "account",
    component: () => <ManageAccount />,
  },
  {
    icon: Lock,
    label: "Приватность",
    description: "Управление приватностью и безопасностью",
    key: "privacy",
    component: () => <ManagePrivacy />,
  },
  {
    icon: Smartphone,
    label: "Устройства",
    description: "Управление устройствами с доступом к аккаунту",
    key: "devices",
    component: () => <ManageDevices />,
  },
  {
    icon: Bell,
    label: "Уведомления",
    description: "Настройка уведомлений о сообщениях и событиях",
    key: "notifications",
    component: () => <ManageNotifications />,
  },
  {
    icon: Video,
    label: "Звук и видео",
    description: "Настройка параметров звука и видео для звонков",
    key: "sound-video",
    component: () => <ManageMedia />,
  },
];
