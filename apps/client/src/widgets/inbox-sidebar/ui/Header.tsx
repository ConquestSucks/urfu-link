import { SearchBar } from "@/shared/ui";
import { BellIcon, ChatCircleTextIcon   } from "phosphor-react-native";
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

        <View className="bg-[#FFFFFF]/5 border gap-1 border-[#FFFFFF]/5 rounded-2xl flex-row w-fit p-[5px]">
          <Pressable className={`p-2 rounded-xl transition-colors duration-300 ${activeTab === "chats"
            ? "bg-[#2B7FFF] shadow-[0_10px_15px_-3px_rgba(43,127,255,0.2),0_4px_6px_-3px_rgba(43,127,255,0.2)]"
            : "hover:bg-white/5"}`} onPress={() => onTabChange("chats")}>
            <ChatCircleTextIcon   size={18} color={activeTab === "chats" ? "#FFFFFF" : "#62748E"}/>
          </Pressable>

          <Pressable className={`p-2 rounded-xl transition-colors duration-300 ${activeTab === "notifications"
            ? "bg-[#2B7FFF] shadow-[0_10px_15px_-3px_rgba(43,127,255,0.2),0_4px_6px_-3px_rgba(43,127,255,0.2)]"
            : "hover:bg-white/5"}`} onPress={() => onTabChange("notifications")}>
            <BellIcon size={18} color={activeTab === "notifications" ? "#FFFFFF" : "#62748E"}/>
          </Pressable>
        </View>
      </View>
      <SearchBar />
    </View>);
};
