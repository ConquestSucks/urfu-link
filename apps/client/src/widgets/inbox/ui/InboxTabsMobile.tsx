import React from "react";
import { Pressable, Text, View } from "react-native";
import { TabType } from "@/entities/tab";

interface InboxTabsMobileProps {
    currentTab: TabType;
    onTabChange: (tab: TabType) => void;
}

export const InboxTabsMobile = ({ currentTab, onTabChange }: InboxTabsMobileProps) => {
    return (
        <View className="flex-row bg-white/5 rounded-full p-1 border border-white/[0.03]">
            <Pressable
                onPress={() => onTabChange("chats")}
                className={`flex-1 py-2 rounded-full items-center justify-center ${
                    currentTab === "chats"
                        ? "bg-brand-650 text-white shadow-lg shadow-brand-650/20"
                        : "text-text-subtle active:text-white"
                }`}
            >
                <Text
                    className={`text-[13px] font-semibold ${
                        currentTab === "chats" ? "text-white" : "text-text-subtle"
                    }`}
                >
                    Личные
                </Text>
            </Pressable>
            <Pressable
                onPress={() => onTabChange("subjects")}
                className={`flex-1 py-2 rounded-full items-center justify-center ${
                    currentTab === "subjects"
                        ? "bg-brand-650 text-white shadow-lg shadow-brand-650/20"
                        : "text-text-subtle active:text-white"
                }`}
            >
                <Text
                    className={`text-[13px] font-semibold ${
                        currentTab === "subjects" ? "text-white" : "text-text-subtle"
                    }`}
                >
                    Дисциплины
                </Text>
            </Pressable>
        </View>
    );
};
