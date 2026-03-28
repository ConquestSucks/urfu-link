import { Href } from "expo-router";
import { BookOpen, MessageSquare } from "lucide-react-native";

export const MENU_ITEMS = [
  { icon: MessageSquare, label: "Личные чаты", href: "/chats" as Href },
  { icon: BookOpen, label: "Дисциплины", href: "/subjects" as Href },
];