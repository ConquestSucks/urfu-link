import { Logo } from "@/shared/ui";
import { BellIcon, ChatCircleTextIcon   } from "phosphor-react-native";
import { router, type Href } from "expo-router";
import React from "react";
import { Pressable, Text, View } from "react-native";

type InboxListMode = "messages" | "notifications";

type MobileHeaderProps = {
    inboxListMode?: InboxListMode;
    onInboxListModeChange?: (mode: InboxListMode) => void;
};

export const MobileHeader = ({
    inboxListMode = "messages",
    onInboxListModeChange,
}: MobileHeaderProps) => {
    const messagesActive = inboxListMode === "messages";
    const notificationsActive = inboxListMode === "notifications";

    return (
        <View className="flex-row items-center justify-between px-4 py-1.5">
            <View className="flex-row items-center gap-2">
                <Logo size={28} />
                <Text className="text-white text-lg font-extrabold tracking-tight">URFU LINK</Text>
            </View>

            <View className="flex-row items-center gap-1">
                <Pressable
                    onPress={() => {
                        onInboxListModeChange?.("messages");
                        router.push("/chats" as Href);
                    }}
                    hitSlop={8}
                    accessibilityRole="button"
                    accessibilityLabel="Личные чаты"
                    accessibilityState={{ selected: messagesActive }}
                    className="p-2.5"
                >
                    <ChatCircleTextIcon  
                        size={24}
                        color={messagesActive ? "#FFFFFF" : "#8b8fa8"}
                        weight={messagesActive ? "fill" : "regular"}
                    />
                </Pressable>
                <Pressable
                    onPress={() => {
                        onInboxListModeChange?.("notifications");
                        router.push("/chats" as Href);
                    }}
                    hitSlop={8}
                    accessibilityRole="button"
                    accessibilityLabel="Уведомления"
                    accessibilityState={{ selected: notificationsActive }}
                    className="p-2.5"
                >
                    <View className="relative">
                        <BellIcon
                            size={24}
                            color={notificationsActive ? "#FFFFFF" : "#8b8fa8"}
                            weight={notificationsActive ? "fill" : "regular"}
                        />
                        <View className="absolute top-0 right-0 w-2 h-2 bg-red-500 rounded-full border border-[#080D1D]" />
                    </View>
                </Pressable>
            </View>
        </View>
    );
};
