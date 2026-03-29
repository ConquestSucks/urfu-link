import React from "react";
import { View } from "react-native";

export const InboxChatSkeleton = () => {
    return (
        <View className="flex-row items-center gap-3 px-4 py-3 md:rounded-2xl md:px-4">
            <View className="h-12 w-12 shrink-0 rounded-xl bg-white/10 animate-pulse" />

            <View className="min-w-0 flex-1 gap-2">
                <View className="flex-row items-start justify-between gap-2">
                    <View className="mt-0.5 h-4 max-w-[72%] flex-1 rounded bg-white/10 animate-pulse" />
                    <View className="mt-0.5 h-3 w-10 shrink-0 rounded bg-white/5 animate-pulse" />
                </View>

                <View className="flex-row items-center gap-2 min-w-0">
                    <View className="h-3.5 min-w-0 flex-1 rounded bg-white/5 animate-pulse" />
                </View>
            </View>
        </View>
    );
};
