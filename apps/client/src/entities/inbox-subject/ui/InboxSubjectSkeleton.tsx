import { InboxChatSkeleton } from "@/entities/inbox-chat/ui/InboxChatSkeleton";
import React from "react";
import { View } from "react-native";

export const InboxSubjectSkeleton = () => {
  return (
    <View className="gap-1 mb-2">
      <View className="h-12 px-4 justify-center">
        <View className="h-[10px] w-32 bg-white/10 rounded animate-pulse" />
      </View>

      <View className="gap-2">
        <InboxChatSkeleton />
        <InboxChatSkeleton />
      </View>
    </View>
  );
};
