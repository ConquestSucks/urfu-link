import React from "react";
import { Pressable, Text, View } from "react-native";
import { TabType } from "../model/components";

interface InboxTabsMobileProps {
  activeTab: TabType;
  onTabChange: (tab: TabType) => void;
}

export const InboxTabsMobile = ({
  activeTab,
  onTabChange,
}: InboxTabsMobileProps) => {
  return (
    <View className="flex-row bg-white/5 rounded-full p-1 border border-white/[0.03]">
      <Pressable
        onPress={() => onTabChange("chats")}
        className={`flex-1 py-2 rounded-full items-center justify-center ${activeTab === "chats" ? "bg-[#2563EB] text-white shadow-lg shadow-[#2563EB]/20" : "text-[#8B8FA8] active:text-white"
          }`}
      >
        <Text
          className={`text-[13px] font-semibold ${activeTab === "chats" ? "text-white" : "text-[#8b8fa8]"
            }`}
        >
          Личные
        </Text>
      </Pressable>
      <Pressable
        onPress={() => onTabChange("subjects")}
        className={`flex-1 py-2 rounded-full items-center justify-center ${activeTab === "subjects" ? "bg-[#2563EB] text-white shadow-lg shadow-[#2563EB]/20" : "text-[#8B8FA8] active:text-white"
          }`}
      >
        <Text
          className={`text-[13px] font-semibold ${activeTab === "subjects" ? "text-white" : "text-[#8b8fa8]"
            }`}
        >
          Дисциплины
        </Text>
      </Pressable>
    </View>
  );
};
