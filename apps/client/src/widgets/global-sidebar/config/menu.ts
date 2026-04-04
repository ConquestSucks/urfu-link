import { Href } from "expo-router";
import { BookOpenIcon, ChatCircleTextIcon   } from "phosphor-react-native";
export const MENU_ITEMS = [
    { icon: ChatCircleTextIcon  , label: "Личные чаты", href: "/chats" as Href },
    { icon: BookOpenIcon, label: "Дисциплины", href: "/subjects" as Href },
];
