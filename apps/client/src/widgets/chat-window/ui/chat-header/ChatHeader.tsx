import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Avatar, StatusIndicator } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import { CaretLeftIcon } from "phosphor-react-native";
import React, { useState } from "react";
import { Pressable, Text, View } from "react-native";
import { ChatHeaderActions } from "./ChatHeaderActions";
import { UserProfileModal } from "./UserProfileModal";
export const ChatHeader = ({ chatId }: { chatId: string }) => {
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const { isMobile } = useWindowSize();
  const chatMeta = useInboxStore((state) => state.getChatById(chatId));
  if (!chatMeta) return null;
  return (
    <>
      <View className="flex-row justify-between items-center border-b border-white/5 pl-2.5 pr-3 py-2">
        <View className="flex-row gap-1 items-center flex-1 min-w-0">
          {isMobile && (
            <Pressable
              onPress={() => safeGoBack("/chats")}
              hitSlop={8}
              className="p-2 rounded-xl"
            >
              <CaretLeftIcon size={24} color="#8B8FA8" weight="bold" />
            </Pressable>
          )}
          <View className="flex-row gap-3 items-center">
            <View className="relative z-1 p-0.5">
              <Avatar size={38} src={chatMeta.avatarUrl} />
              <StatusIndicator
                status="online"
                size={12}
                className="absolute bottom-0 right-0"
              />
            </View>

            <View className="justify-center flex-1 gap-1.5">
              <Text
                numberOfLines={1}
                className="text-white leading-none text-base font-semibold"
              >
                {chatMeta.name}
              </Text>
              <Text
                numberOfLines={1}
                className="text-[#00BC7D] leading-none text-xs font-medium"
              >
                В сети
              </Text>
            </View>
          </View>
        </View>

        <ChatHeaderActions onOpenProfile={() => setIsProfileOpen(true)} />
      </View>

      <UserProfileModal
        isOpen={isProfileOpen}
        onClose={() => setIsProfileOpen(false)}
        user={{ name: chatMeta.name, avatarUrl: chatMeta.avatarUrl }}
      />
    </>
  );
};
