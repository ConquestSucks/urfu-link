import React from "react";
import { Pressable, Text, View } from "react-native";
import { TabType } from "@/entities/tab";
import { Link } from "expo-router";

interface InboxTabsMobileProps {
    currentTab: TabType;
    createHref: (tab: TabType) => any;
}

export const InboxTabsMobile = ({ currentTab, createHref }: InboxTabsMobileProps) => {
    return (
        <View className="flex-row bg-white/5 rounded-full p-1 border border-white/[0.03]">
            <Link href={createHref("chats")} replace asChild>
                <Pressable
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
            </Link>

            <Link href={createHref("subjects")} replace asChild>
                <Pressable
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
            </Link>
        </View>
    );
};