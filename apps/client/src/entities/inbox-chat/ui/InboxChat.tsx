import { Avatar } from "@/shared/ui";
import React from "react";
import { Pressable, Text, View } from "react-native";
import { InboxChatProps } from "../model/types";

export const InboxChat = ({
  avatarUrl,
  name,
  message,
  time,
  isActive,
  onPress,
}: InboxChatProps) => {
  return (
    <Pressable className="select-none" onPress={onPress}>
      <View
        className={`p-[14px] flex-row items-center gap-3 rounded-2xl active:bg-[#2B7FFF]/10 transition-all duration-300 ${
          isActive ? "bg-[#2B7FFF]/10" : "hover:bg-white/5"
        }`}
      >
        <Avatar size={48} src={avatarUrl} />

        <View className="flex-1 gap-[2.5px]">
          <View className="flex-row justify-between">
            <Text
              numberOfLines={1}
              className={`flex-1 text-sm leading-[21px] font-medium ${
                isActive ? "text-white" : "text-[#E2E8F0]"
              }`}
            >
              {name}
            </Text>
            <Text className="text-[10px] font-medium text-[#62748E] mt-1 shrink-0">
              {time}
            </Text>
          </View>

          <Text
            numberOfLines={1}
            className="text-xs leading-[20px] text-[#90A1B9]"
          >
            {message}
          </Text>
        </View>
      </View>
    </Pressable>
  );
};
