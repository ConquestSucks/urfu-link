import React from "react";
import { Text, View } from "react-native";
import { InboxNotificationProps } from "../model/types";

export const InboxNotification = ({
  title,
  time,
  description,
}: InboxNotificationProps) => {
  return (
    <View className="flex-row gap-4 p-4 bg-white/5 border border-white/5 rounded-2xl select-none">
      <View className="bg-[#0F172B]/50 border w-10 h-10 border-white/5 rounded-xl"></View>
      <View className="gap-1 flex-1">
        <View className="flex-row justify-between">
          <Text className="text-sm font-semibold text-white">{title}</Text>
          <Text className="text-[10px] text-[#62748E] font-medium mt-[2.5px]">
            {time}
          </Text>
        </View>
        <View className="h-[39px]">
          <Text className="text-xs text-[#90A1B9] mr-1">{description}</Text>
        </View>
      </View>
    </View>
  );
};
