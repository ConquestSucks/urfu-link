import { useWindowSize } from "@/shared/lib/useWindowSize";
import { SidebarDesktop } from "@/widgets/sidebar-desktop";
import { MobileBottomTabs } from "@/widgets/bottom-tabs-mobile";
import { Slot, useSegments } from "expo-router";
import { lazy, Suspense, useEffect, useState } from "react";
import { View } from "react-native";
import type { Edge } from "react-native-safe-area-context";
import { SafeAreaView } from "react-native-safe-area-context";
import { useCurrentUser } from "@/entities/user";
import { configureMessageSounds } from "@/shared/lib/message-sounds";
import { configureBrowserNotifications } from "@/shared/lib/browser-notifications";
import { configureCallSounds } from "@/shared/lib/call-sounds";
import { glow } from "@/shared/ui/glow";

type IdleWindow = Window & {
    requestIdleCallback?: (callback: () => void) => number;
    cancelIdleCallback?: (handle: number) => void;
};

const REALTIME_BOOT_DELAY_MS = 2500;

const scheduleAfterFirstPaint = (callback: () => void) => {
    let cancelIdle: (() => void) | null = null;
    const delayHandle = setTimeout(() => {
        if (typeof window !== "undefined") {
            const idleWindow = window as IdleWindow;
            if (idleWindow.requestIdleCallback && idleWindow.cancelIdleCallback) {
                const idleHandle = idleWindow.requestIdleCallback(callback);
                cancelIdle = () => idleWindow.cancelIdleCallback?.(idleHandle);
                return;
            }
        }

        callback();
    }, REALTIME_BOOT_DELAY_MS);

    return () => {
        clearTimeout(delayHandle);
        cancelIdle?.();
    };
};

const DeferredRealtime = lazy(() => import("@/features/realtime/DeferredRealtime"));
const SettingsDesktop = lazy(() =>
    import("@/widgets/settings-desktop").then((module) => ({
        default: module.SettingsDesktop,
    })),
);

export default function AuthLayout() {
    const segments = useSegments() as string[];
    const isThreadDetail =
        (segments.includes("chats") || segments.includes("subjects")) && segments.includes("[id]");
    const isCallRoute = segments.includes("call") && segments.includes("[id]");
    const { isDesktop, isMobile } = useWindowSize();
    const [isSettingsOpen, setIsSettingsOpen] = useState(false);
    const [isRealtimeReady, setIsRealtimeReady] = useState(false);
    const shouldElevateThreadDetail = isMobile && isThreadDetail;

    const { data: profile } = useCurrentUser();

    const safeAreaEdges: Edge[] = isMobile
        ? ["top", "left", "right", "bottom"]
        : ["top", "left", "right"];

    useEffect(() => {
        configureMessageSounds(profile?.notifications);
        configureCallSounds(profile?.notifications);
        configureBrowserNotifications(profile?.notifications);
    }, [profile?.notifications]);

    useEffect(() => {
        if (isMobile && isSettingsOpen) setIsSettingsOpen(false);
    }, [isMobile, isSettingsOpen]);

    useEffect(() => scheduleAfterFirstPaint(() => setIsRealtimeReady(true)), []);

    return (
        <SafeAreaView className="flex-1 bg-app-bg" edges={safeAreaEdges}>
            <View className="flex-1 flex-row">
                {isDesktop && !isCallRoute && (
                    <SidebarDesktop onSettingsPress={() => setIsSettingsOpen(true)} />
                )}

                <View
                    className="flex-1 min-w-0"
                    style={{
                        zIndex: shouldElevateThreadDetail ? 2 : 1,
                        elevation: shouldElevateThreadDetail ? 4 : 0,
                        ...(shouldElevateThreadDetail ? glow.surfaceStrong : {}),
                    }}
                >
                    <Slot />
                </View>
            </View>

            {isMobile && !isCallRoute && (
                <View
                    style={{
                        position: "absolute",
                        left: 0,
                        right: 0,
                        bottom: 0,
                        zIndex: isThreadDetail ? 0 : 3,
                        elevation: isThreadDetail ? 0 : 1,
                        pointerEvents: "box-none",
                    }}
                >
                    <MobileBottomTabs />
                </View>
            )}

            {isDesktop && isSettingsOpen ? (
                <Suspense fallback={null}>
                    <SettingsDesktop isOpen={isSettingsOpen} onClose={() => setIsSettingsOpen(false)} />
                </Suspense>
            ) : null}

            {isRealtimeReady ? (
                <Suspense fallback={null}>
                    <DeferredRealtime />
                </Suspense>
            ) : null}
        </SafeAreaView>
    );
}
