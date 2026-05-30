import { Logo } from "@/shared/ui";
import { ViewToggle, ViewType } from "@/entities/view";
import type { NotificationScope } from "@/features/notifications";
import React from "react";
import { Text, View } from "react-native";

type MobileHeaderProps = {
    currentView?: ViewType;
    createHref: (view: ViewType) => any;
    notificationScope?: NotificationScope;
};

export const MobileHeader = ({
    currentView = "messages",
    createHref,
    notificationScope = "chats",
}: MobileHeaderProps) => {
    return (
        <View className="flex-row items-center justify-between px-4 py-1.5">
            <View className="flex-row items-center gap-2">
                <Logo size={28} />
                <Text className="text-white text-lg font-extrabold tracking-tight">URFU LINK</Text>
            </View>

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
