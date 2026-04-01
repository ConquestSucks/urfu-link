import { useWindowSize } from "@/shared/lib/useWindowSize";
import { SidebarDesktop } from "@/widgets/sidebar-desktop";
import { MobileBottomTabs } from "@/widgets/bottom-tabs-mobile";
import { SettingsDesktop } from "@/widgets/settings-desktop";
import { Slot, useSegments } from "expo-router";
import { useEffect, useState } from "react";
import { View } from "react-native";
import type { Edge } from "react-native-safe-area-context";
import { SafeAreaView } from "react-native-safe-area-context";

export default function AuthLayout() {
    const segments = useSegments() as string[];
    const isThreadDetail =
        (segments.includes("chats") || segments.includes("subjects")) && segments.includes("[id]");
    const { isDesktop, isMobile } = useWindowSize();
    const [isSettingsOpen, setIsSettingsOpen] = useState(false);

    const safeAreaEdges: Edge[] = isMobile
        ? ["top", "left", "right", "bottom"]
        : ["top", "left", "right"];

    useEffect(() => {
        if (isMobile && isSettingsOpen) setIsSettingsOpen(false);
    }, [isMobile, isSettingsOpen]);

    return (
        <SafeAreaView className="flex-1 bg-app-bg" edges={safeAreaEdges}>
            <View className="flex-1 flex-row">
                {isDesktop && <SidebarDesktop onSettingsPress={() => setIsSettingsOpen(true)} />}

                <View
                    className="flex-1 min-w-0"
                    style={{
                        zIndex: isThreadDetail ? 2 : 1,
                        elevation: isThreadDetail ? 12 : 0,
                    }}
                >
                    <Slot />
                </View>
            </View>

            {isMobile && (
                <View
                    pointerEvents="box-none"
                    style={{
                        position: "absolute",
                        left: 0,
                        right: 0,
                        bottom: 0,
                        zIndex: isThreadDetail ? 0 : 3,
                        elevation: isThreadDetail ? 0 : 3,
                    }}
                >
                    <MobileBottomTabs />
                </View>
            )}

            {isDesktop && (
                <SettingsDesktop isOpen={isSettingsOpen} onClose={() => setIsSettingsOpen(false)} />
            )}
        </SafeAreaView>
    );
}
