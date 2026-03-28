import { Avatar } from "@/shared/ui";
import React from "react";
import { View } from "react-native";

export const InboxChatSkeleton = () => {
  return (
    <View className="p-[14px] flex-row items-center gap-3 rounded-2xl">
      <Avatar size={48} />

      <View className="flex-1 gap-[2.5px]">
        <View className="flex-row justify-between">
          <View className="h-[14px] w-28 bg-white/10 rounded animate-pulse" />

          <View className="h-[10px] w-8 bg-white/5 rounded mt-1 shrink-0 animate-pulse" />
        </View>

        <View className="h-3 w-3/4 bg-white/5 rounded mt-1 animate-pulse" />
      </View>
    </View>
  );
};
