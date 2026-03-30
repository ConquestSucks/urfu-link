import { SearchBar } from "@/shared/ui";
import { BellIcon, ChatCircleTextIcon   } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, Text, View } from "react-native";
import { TabType } from "../model/components";
interface HeaderProps {
    title: string;
    activeTab: TabType;
    onTabChange: (tab: TabType) => void;
}
export const Header = ({ title, activeTab, onTabChange }: HeaderProps) => {
    return (<View className="px-6">
      <View className="py-5 flex-row items-center justify-between">
        <Text numberOfLines={1} className="text-xl text-white font-bold">
          {title}
        </Text>

        <View className="bg-white/5 border gap-1 border-white/5 rounded-2xl flex-row w-fit p-[5px]">
          <Pressable className={`p-2 rounded-xl transition-colors duration-300 ${activeTab === "chats"
            ? "bg-brand-600 shadow-brand-soft"
            : "hover:bg-white/5"}`} onPress={() => onTabChange("chats")}>
            <ChatCircleTextIcon   size={18} className={activeTab === "chats" ? "text-white" : "text-text-placeholder"}/>
          </Pressable>

          <Pressable className={`p-2 rounded-xl transition-colors duration-300 ${activeTab === "notifications"
            ? "bg-brand-600 shadow-brand-soft"
            : "hover:bg-white/5"}`} onPress={() => onTabChange("notifications")}>
            <BellIcon size={18} className={activeTab === "notifications" ? "text-white" : "text-text-placeholder"}/>
          </Pressable>
        </View>
      </View>
      <SearchBar />
    </View>);
};
