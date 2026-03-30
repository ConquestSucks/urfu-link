import React from "react";
import { View } from "react-native";

export const InboxNotificationSkeleton = () => {
    return (
        <View className="flex-row gap-3 px-4 py-3 md:rounded-2xl select-none border-b md:border border-white/5 md:bg-white/5">
            <View className="flex-shrink-0 flex items-center justify-center text-white bg-app-elevated shadow-sm w-12 h-12 rounded-[14px]"></View>

            <View className="min-w-0 flex-1 gap-2">
                <View className="flex-row justify-between gap-2">
                    <View className="h-4 min-w-0 flex-1 rounded bg-white/10 animate-pulse" />
                    <View className="h-3 w-10 shrink-0 rounded bg-white/5 animate-pulse" />
                </View>
                <View className="min-h-[39px] justify-center">
                    <View className="h-3 w-full rounded bg-white/5 animate-pulse" />
                </View>
            </View>
        </View>
    );
};
