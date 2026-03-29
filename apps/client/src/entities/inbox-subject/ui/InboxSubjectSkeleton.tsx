import { InboxChatSkeleton } from "@/entities/inbox-chat/ui/InboxChatSkeleton";
import React from "react";
import { View } from "react-native";

export const InboxSubjectSkeleton = () => {
    return (
        <View>
            <View className="px-4 py-3">
                <View className="h-[10px] w-40 max-w-[85%] rounded bg-white/10 animate-pulse" />
            </View>

            <View className="gap-0 md:gap-1">
                <InboxChatSkeleton />
                <InboxChatSkeleton />
            </View>
        </View>
    );
};
