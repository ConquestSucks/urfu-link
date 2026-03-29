import { useWindowSize } from "@/shared/lib/useWindowSize";
import { GlobalSidebar } from "@/widgets/global-sidebar";
import { MobileBottomTabs } from "@/widgets/mobile-bottom-tabs";
import { SettingsWindow } from "@/widgets/settings-window";
import { Slot, usePathname } from "expo-router";
import { useState } from "react";
import { View } from "react-native";
import type { Edge } from "react-native-safe-area-context";
import { SafeAreaView } from "react-native-safe-area-context";

function isThreadDetailPath(pathname: string) {
    return /\/chats\/[^/]+/.test(pathname) || /\/subjects\/[^/]+/.test(pathname);
}

export default function AuthLayout() {
    const pathname = usePathname();
    const isThreadDetail = isThreadDetailPath(pathname);
    const { isDesktop, isMobile } = useWindowSize();
    const [isSettingsOpen, setIsSettingsOpen] = useState(false);
    const safeAreaEdges: Edge[] = isMobile
        ? ["top", "left", "right", "bottom"]
        : ["top", "left", "right"];

    return (
        <SafeAreaView className="flex-1 bg-[#080D1D]" edges={safeAreaEdges}>
            <View className="flex-1 flex-row">
                {isDesktop && (<GlobalSidebar onSettingsPress={() => setIsSettingsOpen(true)} />)}

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

            <SettingsWindow isOpen={isSettingsOpen} onClose={() => setIsSettingsOpen(false)} />
        </SafeAreaView>
    );
}
