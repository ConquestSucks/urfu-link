import React from "react";
import { View } from "react-native";

export const InboxNotificationSkeleton = () => {
  return (
    <View className="flex-row gap-4 p-4 bg-white/5 border border-white/5 rounded-2xl">
      <View className="w-10 h-10 bg-white/10 rounded-xl animate-pulse" />

      <View className="gap-1 flex-1">
        <View className="flex-row justify-between">
          <View className="h-[14px] w-32 bg-white/10 rounded animate-pulse" />

          <View className="h-[10px] w-8 bg-white/5 rounded mt-[2.5px] shrink-0 animate-pulse" />
        </View>

        <View className="h-[39px] justify-center gap-1.5 mr-1">
          <View className="h-3 w-full bg-white/5 rounded animate-pulse" />
          <View className="h-3 w-4/5 bg-white/5 rounded animate-pulse" />
        </View>
      </View>
    </View>
  );
};
