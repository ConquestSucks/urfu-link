import { Logo } from "@/shared/ui";
import { ViewToggle, ViewType } from "@/entities/view";
import React from "react";
import { Text, View } from "react-native";

type MobileHeaderProps = {
    currentView?: ViewType;
    createHref: (view: ViewType) => any;
};

export const MobileHeader = ({
    currentView = "messages",
    createHref,
}: MobileHeaderProps) => {
    const messagesActive = currentView === "messages";
    const notificationsActive = currentView === "notifications";

    return (
        <View className="flex-row items-center justify-between px-4 py-1.5">
            <View className="flex-row items-center gap-2">
                <Logo size={28} />
                <Text className="text-white text-lg font-extrabold tracking-tight">URFU LINK</Text>
            </View>

            <ViewToggle currentView={currentView} createHref={createHref} />
        </View>
    );
};
