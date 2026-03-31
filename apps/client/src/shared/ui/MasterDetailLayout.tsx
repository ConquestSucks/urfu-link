import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Slot, Stack } from "expo-router";
import React from "react";
import { Platform, StyleSheet, View } from "react-native";

interface MasterDetailLayoutProps {
    sidebar: React.ReactNode;
    isDetailView: boolean;
}

const inboxDetailStackOptions = {
    headerShown: false as const,
    animation: "slide_from_right" as const,
    presentation: "card" as const,
};

export const MasterDetailLayout = ({ sidebar, isDetailView }: MasterDetailLayoutProps) => {
    const isWeb = Platform.OS === "web";
    const { isMobile } = useWindowSize();

    const stack = (
        <Stack screenOptions={{ headerShown: false }}>
            <Stack.Screen name="index" />
            <Stack.Screen name="[id]" options={inboxDetailStackOptions} />
        </Stack>
    );

    if (isMobile) {
        return (
            <View style={styles.flex}>
                {!isDetailView && (
                    <View style={styles.sidebarOverlay} className="bg-app-bg" pointerEvents="box-none">
                        {sidebar}
                    </View>
                )}
                <View
                    style={[
                        styles.flex,
                        !isDetailView && styles.outletHidden,
                    ]}
                    pointerEvents={isDetailView ? "auto" : "none"}
                >
                    {stack}
                </View>
            </View>
        );
    }

    return (
        <View className="flex-1 flex-row">
            {sidebar}

            <View className="flex-1 min-w-0">
                {isWeb ? (<Slot />) : stack}
            </View>
        </View>
    );
};
const styles = StyleSheet.create({
    flex: {
        flex: 1,
    },
    sidebarOverlay: {
        ...StyleSheet.absoluteFillObject,
        zIndex: 10,
    },
    outletHidden: {
        opacity: 0,
    },
});
