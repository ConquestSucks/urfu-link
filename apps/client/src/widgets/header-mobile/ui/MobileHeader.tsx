import { Logo } from "@/shared/ui";
import { ViewToggle, ViewType } from "@/entities/view";
import type { NotificationScope } from "@/features/notifications";
import { Link } from "expo-router";
import React from "react";
import { Pressable, Text, View } from "react-native";

type MobileHeaderProps = {
    currentView?: ViewType;
    createHref: (view: ViewType) => any;
    notificationScope?: NotificationScope;
};

const homeInboxHref = {
    pathname: "/chats",
    params: { view: "messages" },
} as const;

export const MobileHeader = ({
    currentView = "messages",
    createHref,
    notificationScope = "chats",
}: MobileHeaderProps) => {
    return (
        <View className="flex-row items-center justify-between px-4 py-1.5">
            <Link href={homeInboxHref} replace asChild>
                <Pressable
                    accessibilityLabel="Home"
                    accessibilityRole="link"
                    className="flex-row items-center gap-2 rounded-xl"
                    style={({ pressed }) => ({ opacity: pressed ? 0.72 : 1 })}
                >
                    <Logo size={28} />
                    <Text className="text-white text-lg font-extrabold tracking-tight">URFU LINK</Text>
                </Pressable>
            </Link>

            <View className="flex-row items-center gap-2">
                <ViewToggle
                    currentView={currentView}
                    createHref={createHref}
                    notificationScope={notificationScope}
                />
            </View>
        </View>
    );
};
