import { router, usePathname } from "expo-router";
import React, { useEffect } from "react";
import { StyleSheet } from "react-native";
import Animated, { Easing, useAnimatedStyle, useSharedValue, withTiming, } from "react-native-reanimated";
import { MOBILE_TAB_BAR_HEIGHT } from "../config/layout";
import { MOBILE_TABS } from "../config/tabs";
import { MobileBottomTabItem } from "./MobileBottomTabItem";
const TIMING_MS = 320;
function isChatDetailPath(pathname: string) {
    return (/\/chats\/[^/]+/.test(pathname) || /\/subjects\/[^/]+/.test(pathname));
}
function TabItems({ pathname }: {
    pathname: string;
}) {
    return (<>
      {MOBILE_TABS.map((tab) => {
            const isChatsOrInbox = tab.href === "/chats" &&
                (pathname === "/chats" ||
                    pathname.startsWith("/chats/") ||
                    pathname === "/notifications" ||
                    pathname.startsWith("/subjects"));
            const isActive = isChatsOrInbox ||
                (tab.href !== "/chats" &&
                    (pathname === tab.href ||
                        pathname.startsWith(`${tab.href}/`)));
            return (<MobileBottomTabItem key={tab.href as string} icon={tab.icon} label={tab.label} isActive={isActive} onPress={() => router.replace(tab.href)}/>);
        })}
    </>);
}
export const MobileBottomTabs = () => {
    const pathname = usePathname();
    const hideTabs = isChatDetailPath(pathname);
    const visible = useSharedValue(hideTabs ? 0 : 1);

    useEffect(() => {
        visible.value = withTiming(hideTabs ? 0 : 1, {
            duration: TIMING_MS,
            easing: Easing.bezier(0.4, 0, 0.2, 1),
        });
    }, [hideTabs]);

    const containerStyle = useAnimatedStyle(() => ({
        height: MOBILE_TAB_BAR_HEIGHT * visible.value,
        overflow: "hidden",
    }));
    const rowStyle = useAnimatedStyle(() => ({
        transform: [{ translateY: (1 - visible.value) * MOBILE_TAB_BAR_HEIGHT }],
    }));

    return (
        <Animated.View style={containerStyle} pointerEvents={hideTabs ? "none" : "auto"}>
            <Animated.View style={[styles.row, { height: MOBILE_TAB_BAR_HEIGHT }, rowStyle]}>
                <TabItems pathname={pathname} />
            </Animated.View>
        </Animated.View>
    );
};
const styles = StyleSheet.create({
    row: {
        flexDirection: "row",
        backgroundColor: "#080D1D",
        borderTopWidth: 1,
        borderTopColor: "rgba(255,255,255,0.05)",
        paddingBottom: 2,
    },
});
