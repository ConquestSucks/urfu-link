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
        className={`flex-1 py-2 rounded-full items-center justify-center ${activeTab === "chats" ? "bg-brand-650 text-white shadow-lg shadow-brand-650/20" : "text-text-subtle active:text-white"
          }`}
      >
        <Text
          className={`text-[13px] font-semibold ${activeTab === "chats" ? "text-white" : "text-text-subtle"
            }`}
        >
          Личные
        </Text>
      </Pressable>
      <Pressable
        onPress={() => onTabChange("subjects")}
        className={`flex-1 py-2 rounded-full items-center justify-center ${activeTab === "subjects" ? "bg-brand-650 text-white shadow-lg shadow-brand-650/20" : "text-text-subtle active:text-white"
          }`}
      >
        <Text
          className={`text-[13px] font-semibold ${activeTab === "subjects" ? "text-white" : "text-text-subtle"
            }`}
        >
          Дисциплины
        </Text>
      </Pressable>
    </View>
  );
};
