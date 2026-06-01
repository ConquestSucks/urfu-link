import { AuthGate } from "@/features/auth";
import { useCurrentUser } from "@/entities/user";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { configureBrowserNotifications } from "@/shared/lib/browser-notifications";
import { configureCallSounds } from "@/shared/lib/call-sounds";
import { configureMessageSounds } from "@/shared/lib/message-sounds";
import { SidebarDesktop } from "@/widgets/sidebar-desktop";
import { Slot, useSegments } from "expo-router";
import { lazy, Suspense, useEffect, useState } from "react";
import { View } from "react-native";
import type { Edge } from "react-native-safe-area-context";
import { SafeAreaView } from "react-native-safe-area-context";

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

function AuthorizedLayoutContent() {
    const segments = useSegments() as string[];
    const isCallRoute = segments.includes("call") && segments.includes("[id]");
    const { isDesktop, isMobile } = useWindowSize();
    const [isSettingsOpen, setIsSettingsOpen] = useState(false);
    const [isRealtimeReady, setIsRealtimeReady] = useState(false);

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

                <View className="flex-1 min-w-0">
                    <Slot />
                </View>
            </View>

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

export default function AuthLayout() {
    return (
        <AuthGate>
            <AuthorizedLayoutContent />
        </AuthGate>
    );
}
