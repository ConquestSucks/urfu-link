import React from "react";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Redirect, Slot, usePathname, useRouter } from "expo-router";
import { View, Text, Pressable, ScrollView } from "react-native";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import { SETTINGS_ITEMS } from "@/shared/config";
import { MOBILE_TAB_BAR_HEIGHT } from "@/shared/config";

const getHeaderTitle = (pathname: string) => {
    const segments = pathname.split("/").filter(Boolean);

    const currentKey = segments[segments.length - 1];

    const currentItem = SETTINGS_ITEMS.find((item) => item.key === currentKey);

    return currentItem ? currentItem.label : "Настройки";
};

export default function ProfileGroupLayout() {
    const { isMobile } = useWindowSize();
    const pathname = usePathname();
    const router = useRouter();

    if (!isMobile) return <Redirect href="/chats" />;

    const isRootProfile = pathname === "/profile";

    const handleGoBack = () => {
        if (router.canGoBack()) {
            router.back();
        } else {
            router.push("/profile");
        }
    };

    return (
        <View className="flex-1 bg-app-bg gap-6">
            {!isRootProfile && (
                <View className="flex-row gap-6 items-center px-6 py-3.5 border-b border-white/5">
                    <Pressable onPress={handleGoBack}>
                        <CaretLeftIcon size={20} className="text-white" />
                    </Pressable>
                    <Text className="text-white text-lg font-bold">{getHeaderTitle(pathname)}</Text>
                </View>
            )}

            <ScrollView
                contentContainerStyle={{ paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT }}
                className="px-6 overflow-y-scroll"
            >
                <Slot />
            </ScrollView>
        </View>
    );
}
