import { ChatCircleTextIcon , UserCircleIcon } from "phosphor-react-native";
import { TabConfig } from "../model/types";
export const MOBILE_TABS: TabConfig[] = [
    {
        icon: ChatCircleTextIcon ,
        label: "Чаты",
        href: "/chats",
    },
    {
        icon: UserCircleIcon,
        label: "Профиль",
        href: "/profile",
    },
];
