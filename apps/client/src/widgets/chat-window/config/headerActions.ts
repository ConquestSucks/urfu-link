import {
  Ban,
  BellOff,
  Pin,
  Search,
  Trash2,
  UserCircle,
} from "lucide-react-native";

export const getChatHeaderActions = (callbacks: {
  onOpenProfile: () => void;
}) => [
  {
    icon: UserCircle,
    iconColor: "#51A2FF",
    label: "Профиль пользователя",
    command: callbacks.onOpenProfile,
  },
  {
    icon: Search,
    iconColor: "#00D492",
    label: "Поиск по сообщениям",
    command: () => console.log("Ищем..."),
  },
  {
    icon: Pin,
    iconColor: "#FFB900",
    label: "Закрепленные",
    command: () => console.log("Закрепленные..."),
  },
  { separator: true },
  {
    icon: BellOff,
    iconColor: "#90A1B9",
    label: "Отключить уведомления",
    command: () => console.log("Уведомления..."),
  },
  {
    icon: Trash2,
    iconColor: "#FF8904",
    label: "Очистить историю",
    command: () => console.log("Чистим..."),
  },
  { separator: true },
  {
    icon: Ban,
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
    icon: UserCircle,
    iconColor: "#51A2FF",
    label: "Участники",
    command: callbacks.onOpenMembers,
  },
  {
    icon: Search,
    iconColor: "#00D492",
    label: "Поиск по сообщениям",
    command: () => console.log("Ищем..."),
  },
  {
    icon: Pin,
    iconColor: "#FFB900",
    label: "Закрепленные",
    command: () => console.log("Закрепленные..."),
  },
  { separator: true },
  {
    icon: BellOff,
    iconColor: "#90A1B9",
    label: "Отключить уведомления",
    command: () => console.log("Уведомления..."),
  },
];
