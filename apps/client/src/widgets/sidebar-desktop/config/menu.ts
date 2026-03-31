import { Href } from "expo-router";
import { BookOpenIcon, ChatCircleTextIcon   } from "@/shared/ui/phosphor";
export const MENU_ITEMS = [
    { icon: ChatCircleTextIcon  , label: "Личные чаты", href: "/chats" as Href },
    { icon: BookOpenIcon, label: "Дисциплины", href: "/subjects" as Href },
];
