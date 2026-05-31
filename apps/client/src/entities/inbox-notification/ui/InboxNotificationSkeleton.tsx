import React from "react";
import { View } from "react-native";

export const InboxNotificationSkeleton = () => {
    return (
        <View className="flex-row gap-2.5 px-3 py-2.5 md:rounded-lg select-none border-b md:border border-white/[0.04] md:bg-white/[0.025]">
            <View className="mt-0.5 flex-shrink-0 bg-app-elevated w-8 h-8 rounded-lg animate-pulse" />

            <View className="min-w-0 flex-1 gap-1.5">
                <View className="flex-row justify-between items-center gap-2">
                    <View className="h-3.5 min-w-0 flex-1 rounded bg-white/10 animate-pulse" />
                    <View className="h-3 w-10 shrink-0 rounded bg-white/5 animate-pulse" />
                </View>
                <View className="h-3 w-full rounded bg-white/5 animate-pulse" />
                <View className="h-3 w-2/3 rounded bg-white/5 animate-pulse" />
            </View>
        </View>
    );
};
