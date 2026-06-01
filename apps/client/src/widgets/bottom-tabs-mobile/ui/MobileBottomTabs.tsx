import { router, usePathname } from "expo-router";
import React from "react";
import { View } from "react-native";
import { MOBILE_TAB_BAR_HEIGHT } from "@/shared/config";
import { MOBILE_TABS } from "../config/tabs";
import { MobileBottomTabItem } from "./MobileBottomTabItem";
import { glow } from "@/shared/ui/glow";

function TabItems({ pathname }: { pathname: string }) {
    return (
        <>
            {MOBILE_TABS.map((tab) => {
                const isChatsOrInbox =
                    tab.href === "/chats" &&
                    (pathname === "/chats" ||
                        pathname.startsWith("/chats/") ||
                        pathname.startsWith("/subjects"));
                const isActive =
                    isChatsOrInbox ||
                    (tab.href !== "/chats" &&
                        (pathname === tab.href || pathname.startsWith(`${tab.href}/`)));
                return (
                    <MobileBottomTabItem
                        key={tab.href as string}
                        icon={tab.icon}
                        label={tab.label}
                        isActive={isActive}
                        onPress={() => router.replace(tab.href)}
                    />
                );
            })}
        </>
    );
}

export const MobileBottomTabs = () => {
    const pathname = usePathname();

    return (
        <View
            testID="mobile-bottom-tabs-container"
            className="flex-row bg-app-bg border-t border-effects-white05 pb-[2px]"
            style={{ height: MOBILE_TAB_BAR_HEIGHT, ...glow.surface }}
        >
            <TabItems pathname={pathname} />
        </View>
    );
};
