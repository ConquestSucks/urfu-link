import React from "react";
import { Text, View } from "react-native";
import { InboxNotificationProps } from "../model/types";
import { InfoIcon } from "phosphor-react-native";

export const InboxNotification = ({
  title,
  time,
  description,
  isRead = true,
}: InboxNotificationProps) => {
  const unread = isRead === false;

  return (
    <View
      className={`flex-row gap-3 px-4 py-3 md:rounded-2xl select-none border-b md:border border-white/5 md:bg-white/5`}
    >
      <View className="flex-shrink-0 flex items-center justify-center text-white bg-[#1A1D27] border border-white/[0.1] shadow-sm w-12 h-12 rounded-[14px]">
        <InfoIcon size={24} color="#8B8FA8" weight="bold" />
      </View>

      <View className="gap-2 flex-1 min-w-0">
        <View className="flex-row justify-between gap-2">
          <Text
            numberOfLines={1}
            className={`leading-none text-base flex-1 min-w-0 text-white font-semibold`}
          >
            {title}
          </Text>
          <Text className="text-xs font-medium text-[#8B8FA8]">
            {time}
          </Text>
        </View>
        <View className="min-h-[39px] justify-center">
          <Text
            className={`text-xs mr-1 ${unread ? "text-[#CBD5E1]" : "text-[#90A1B9]"}`}
          >
            {description}
          </Text>
        </View>
      </View>
    </View>
  );
};
